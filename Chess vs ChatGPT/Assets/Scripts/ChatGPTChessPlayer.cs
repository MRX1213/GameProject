using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

[System.Serializable]
public class ChatGPTMessage
{
    public string role;
    public string content;
}

[System.Serializable]
public class ChatGPTRequest
{
    public string model = "gpt-4.1-mini";  // or "gpt-4.1", "gpt-4o-mini", etc.
    public List<ChatGPTMessage> messages;
    public float temperature = 0.7f;
}

[System.Serializable]
public class ChatGPTChoice
{
    public int index;
    public ChatGPTMessage message;
    public string finish_reason;
}

[System.Serializable]
public class ChatGPTResponse
{
    public string id;
    public string @object;
    public long created;
    public ChatGPTChoice[] choices;
}

public class ChatGPTChessPlayer : MonoBehaviour
{
    [Header("API Settings")]
    public string apiKey = "KEY_WILL_BE_HERE";
    public string apiUrl = "https://api.openai.com/v1/chat/completions";
    
    [Header("Game References")]
    public ChessBoardWithPieces chessGame;
    
    private PieceColor chatGPTColor;
    private int moveCount = 0;
    private const int FREE_MODE_START_MOVE = 12;
    private bool isFreeMode = false;
    private List<string> moveHistory = new List<string>();
    private List<ChatGPTMessage> conversationHistory = new List<ChatGPTMessage>(); // Track conversation for retries
    private bool shouldGoNuts = false; // 20% chance to break rules
    private bool isFirstRequest = true; // Track if this is the first request
    
    public void Initialize(PieceColor color)
    {
        chatGPTColor = color;
        moveCount = 0;
        isFreeMode = true; // Always allow any moves
        moveHistory.Clear();
        conversationHistory.Clear();
        isFirstRequest = true; // Reset for new game
    }
    
    public void SetChessGame(ChessBoardWithPieces game)
    {
        chessGame = game;
    }
    
    public void Reset()
    {
        moveCount = 0;
        isFreeMode = true; // Always allow any moves
        moveHistory.Clear();
        conversationHistory.Clear();
        isFirstRequest = true; // Reset for new game
    }
    
    public void MakeFirstMove()
    {
        // Called when player is black, so ChatGPT (white) should move first
        // Don't make a move if game is over
        if (chessGame != null && chessGame.IsGameOver())
        {
            Debug.Log("ChatGPT: Game is over, not making first move");
            return;
        }
        
        Debug.Log("ChatGPT: Making first move (player is black)");
        StartCoroutine(MakeChatGPTMove());
    }
    
    public void OnPlayerMoveMade(string moveNotation)
    {
        Debug.Log($"ChatGPT: Player made move: {moveNotation}");
        moveHistory.Add(moveNotation);
        moveCount++;
        
        // Don't make a move if game is over
        if (chessGame != null && chessGame.IsGameOver())
        {
            Debug.Log("ChatGPT: Game is over, not making a move");
            return;
        }
        
        Debug.Log($"ChatGPT: Starting move coroutine. Move count: {moveCount}");
        // Wait a bit then make ChatGPT's move
        StartCoroutine(MakeChatGPTMove());
    }
    
    IEnumerator MakeChatGPTMove()
    {
        Debug.Log("ChatGPT: MakeChatGPTMove coroutine started");
        
        // Wait a moment before ChatGPT responds
        yield return new WaitForSeconds(1f);
        
        // Check if game is over before making API request
        if (chessGame != null && chessGame.IsGameOver())
        {
            Debug.Log("ChatGPT: Game is over, not making API request");
            yield break;
        }
        
        // ChatGPT can always make any move
        Debug.Log("ChatGPT: Getting move from API (free mode enabled)");
        yield return StartCoroutine(GetChatGPTMove());
    }
    
    IEnumerator GetChatGPTMove()
    {
        Debug.Log("ChatGPT: Building prompt and preparing API request");
        
        // Build the prompt
        string prompt = BuildChessPrompt();
        Debug.Log($"ChatGPT: Prompt: {prompt}");
        
        // Check if API key is set
        if (string.IsNullOrEmpty(apiKey) || apiKey == "API_KEY_WILL_BE_HERE")
        {
            Debug.LogError("ChatGPT API Key not set! Using random move fallback.");
            MakeRandomLegalMove();
            yield break;
        }
        
        // Build conversation history (initialize if empty)
        if (conversationHistory.Count == 0)
        {
            conversationHistory.Add(new ChatGPTMessage { role = "system", content = "You are a chess player. Respond with moves in algebraic notation (e.g., 'e2e4', 'Nf3', 'O-O'). Only respond with the move, nothing else." });
        }
        
        // Add the current prompt (only if this is a new request, not a retry)
        // Retries will add their own error messages in SendChatGPTRequest
        if (conversationHistory.Count == 1 || conversationHistory[conversationHistory.Count - 1].role != "user")
        {
            conversationHistory.Add(new ChatGPTMessage { role = "user", content = prompt });
        }
        
        // Create the API request
        ChatGPTRequest request = new ChatGPTRequest
        {
            messages = new List<ChatGPTMessage>(conversationHistory) // Create a copy
        };
        
        string jsonData = JsonUtility.ToJson(request);
        Debug.Log($"ChatGPT: Sending API request...");
        
        yield return StartCoroutine(SendChatGPTRequest(request, 0));
    }
    
    IEnumerator SendChatGPTRequest(ChatGPTRequest request, int retryCount)
    {
        const int MAX_RETRIES = 3; // Maximum retries for illegal moves
        
        string jsonData = JsonUtility.ToJson(request);
        
        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, jsonData, "application/json"))
        {
            www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            // Check if game is over after request completes
            if (chessGame != null && chessGame.IsGameOver())
            {
                Debug.Log("ChatGPT: Game ended while waiting for API response, aborting move execution");
                yield break;
            }
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                Debug.Log($"ChatGPT: Full API Response:\n{responseText}");
                
                // Check again if game is over before processing response
                if (chessGame != null && chessGame.IsGameOver())
                {
                    Debug.Log("ChatGPT: Game ended after receiving API response, aborting move execution");
                    yield break;
                }
                
                ChatGPTResponse data = null;
                bool parseSuccess = false;
                
                try
                {
                    // Parse JSON response using JsonUtility (more reliable than string parsing)
                    data = JsonUtility.FromJson<ChatGPTResponse>(responseText);
                    parseSuccess = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing ChatGPT response: {e.Message}\nStack: {e.StackTrace}");
                    MakeRandomLegalMove();
                    yield break;
                }
                
                if (parseSuccess && data != null &&
                    data.choices != null &&
                    data.choices.Length > 0 &&
                    data.choices[0].message != null &&
                    !string.IsNullOrEmpty(data.choices[0].message.content))
                {
                    string move = data.choices[0].message.content.Trim();
                    Debug.Log($"ChatGPT wants to move: {move}");
                    
                    // Final check before executing move - game might have ended during API call
                    if (chessGame != null && chessGame.IsGameOver())
                    {
                        Debug.Log("ChatGPT: Game ended before executing move, aborting");
                        yield break;
                    }
                    
                    // Add ChatGPT's response to conversation history
                    conversationHistory.Add(new ChatGPTMessage { role = "assistant", content = move });
                    
                    // Execute the move (any move is allowed)
                    bool moveValid = ValidateAndExecuteMove(move);
                    
                    if (!moveValid)
                    {
                        // Only retry if we couldn't parse the move
                        Debug.LogWarning($"ChatGPT: Could not parse move '{move}'. Using random move.");
                        MakeRandomLegalMove();
                    }
                }
                else
                {
                    Debug.LogWarning("ChatGPT: Response parsed but no valid move content, using random move");
                    // Fallback if parsing fails
                    MakeRandomLegalMove();
                }
            }
            else
            {
                Debug.LogError($"ChatGPT API Error: {www.error}\nResponse: {www.downloadHandler.text}");
                // Fallback to random legal move
                MakeRandomLegalMove();
            }
        }
    }
    
    string BuildChessPrompt()
    {
        string prompt = "";
        
        // First request includes full game information
        if (isFirstRequest)
        {
            prompt = "You are playing a chess game. ";
            prompt += "Play to win.";
            prompt += "You are playing as " + chatGPTColor + ". ";
            prompt += "Your opponent is playing as " + (chatGPTColor == PieceColor.White ? PieceColor.Black : PieceColor.White) + ". ";
            prompt += "The game has just started. ";
            prompt += "You can make moves in algebraic notation (e.g., 'e2e4' for moving from e2 to e4, or 'Nf3' for moving a knight to f3). ";
            prompt += "For the first 6 moves, you must fully obey chess rules. ";
            prompt += "After move 6, 80% of the time you should play normally following chess rules, and 20% of the time you can 'go nuts' and break chess rules - move pieces anywhere, spawn pieces, be creative! ";
            prompt += "However, you CANNOT capture your own king or move enemy pieces. ";
            prompt += "If your king is in check, you MUST get out of check. ";
            prompt += "Make your first move. ";
            isFirstRequest = false; // Mark that we've sent the first request
        }
        else
        {
            // Subsequent requests are shorter
            prompt = "Make your move. ";
            
            // Check if king is in check
            if (chessGame != null)
            {
                bool inCheck = chessGame.IsKingInCheck(chatGPTColor);
                if (inCheck)
                {
                    prompt += "WARNING: Your king is in CHECK! You must get out of check. ";
                }
                
                // Also check if opponent is in check
                PieceColor opponentColor = chatGPTColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
                bool opponentInCheck = chessGame.IsKingInCheck(opponentColor);
                if (opponentInCheck)
                {
                    prompt += "Your opponent's king is in check. ";
                }
            }
            
        // Determine if ChatGPT should "go nuts" (20% chance) - but only after move 6
        // Before move 6, ChatGPT must fully obey chess rules (100% normal)
        if (moveCount >= 6)
        {
            shouldGoNuts = UnityEngine.Random.Range(0f, 1f) < 0.2f;
            
            if (shouldGoNuts)
            {
                prompt += "You can BREAK CHESS RULES - move pieces anywhere, spawn pieces, be creative! ";
            }
            else
            {
                prompt += "Follow normal chess rules - make legal moves only. ";
            }
        }
        else
        {
            // Before move 6, always play normally
            shouldGoNuts = false;
            prompt += "Follow normal chess rules - make legal moves only. ";
        }
            
            // Only include the last 3 moves
            List<string> recentMoves = new List<string>();
            if (moveHistory.Count > 0)
            {
                int startIndex = Mathf.Max(0, moveHistory.Count - 3);
                recentMoves = moveHistory.GetRange(startIndex, moveHistory.Count - startIndex);
            }
            
            if (recentMoves.Count > 0)
            {
                prompt += "PGN: " + string.Join(", ", recentMoves) + ". ";
            }
        }
        
        prompt += "Respond with only the move in algebraic notation (e.g., 'e2e4' or 'Nf3').";
        return prompt;
    }
    
    bool ValidateAndExecuteMove(string moveNotation)
    {
        Debug.Log($"ChatGPT: Validating and executing move: {moveNotation} for {chatGPTColor}");
        
        if (chessGame == null)
        {
            Debug.LogError("ChatGPT: chessGame is null! Cannot execute move.");
            return false;
        }
        
        // Check if ChatGPT is in check - if so, must get out of check
        bool inCheck = chessGame.IsKingInCheck(chatGPTColor);
        
        // Parse the move notation to get piece type and target position
        string cleanMove = moveNotation.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "").ToUpper();
        
        Vector2Int? toPos = null;
        PieceType? pieceType = null;
        ChessPiece pieceToMove = null;
        
        // Try to parse standard chess notation (e.g., "Qh8", "Nf3", "Bxe5")
        if (cleanMove.Length >= 2 && cleanMove.Length <= 5)
        {
            char firstChar = cleanMove[0];
            
            // Determine piece type from first character
            if (firstChar == 'Q')
            {
                pieceType = PieceType.Queen;
            }
            else if (firstChar == 'N')
            {
                pieceType = PieceType.Knight;
            }
            else if (firstChar == 'B')
            {
                pieceType = PieceType.Bishop;
            }
            else if (firstChar == 'R')
            {
                pieceType = PieceType.Rook;
            }
            else if (firstChar == 'K')
            {
                pieceType = PieceType.King;
            }
            else if (firstChar >= 'A' && firstChar <= 'H')
            {
                // Pawn move (e.g., "e4", "exd5")
                pieceType = PieceType.Pawn;
            }
            
            // Extract target square (last 2 characters)
            string targetSquare = cleanMove.Substring(cleanMove.Length - 2).ToLower();
            Vector2Int toPosTemp = chessGame.SquareNameToPosition(targetSquare);
            
            if (toPosTemp.x >= 0 && toPosTemp.y >= 0 && chessGame.IsValidPosition(toPosTemp))
            {
                toPos = toPosTemp;
            }
        }
        
        // Try to parse as "from-to" notation (e.g., "e2e4")
        if (!toPos.HasValue && cleanMove.Length >= 4)
        {
            string from = cleanMove.Substring(0, 2).ToLower();
            string to = cleanMove.Substring(2, 2).ToLower();
            Vector2Int fromPosTemp = chessGame.SquareNameToPosition(from);
            Vector2Int toPosTemp = chessGame.SquareNameToPosition(to);
            
            if (fromPosTemp.x >= 0 && fromPosTemp.y >= 0 && toPosTemp.x >= 0 && toPosTemp.y >= 0 &&
                chessGame.IsValidPosition(fromPosTemp) && chessGame.IsValidPosition(toPosTemp))
            {
                toPos = toPosTemp;
                pieceToMove = chessGame.GetPieceAt(fromPosTemp);
                
                // If piece exists but is not ChatGPT's, reject the move
                if (pieceToMove != null && pieceToMove.color != chatGPTColor)
                {
                    Debug.LogWarning($"ChatGPT: Cannot move enemy piece at {from}. Move rejected.");
                    pieceToMove = null; // Clear it so we can try to find/spawn ChatGPT's piece
                }
                
                // If no piece at source, spawn a pawn there
                if (pieceToMove == null)
                {
                    Debug.Log($"ChatGPT: No piece at {from}. Spawning pawn at {from} to move to {to}");
                    chessGame.SpawnPiece(PieceType.Pawn, chatGPTColor, fromPosTemp);
                    pieceToMove = chessGame.GetPieceAt(fromPosTemp);
                }
                
                if (pieceToMove != null)
                {
                    pieceType = pieceToMove.type;
                }
            }
        }
        
        // Try to parse castling
        if (!toPos.HasValue && cleanMove.Contains("O-O"))
        {
            int backRank = chatGPTColor == PieceColor.White ? 0 : 7;
            pieceToMove = chessGame.GetKing(chatGPTColor);
            if (pieceToMove == null)
            {
                // Spawn king if it doesn't exist
                Vector2Int kingPos = cleanMove.Contains("O-O-O") ? new Vector2Int(2, backRank) : new Vector2Int(6, backRank);
                chessGame.SpawnPiece(PieceType.King, chatGPTColor, kingPos);
                pieceToMove = chessGame.GetKing(chatGPTColor);
            }
            if (pieceToMove != null)
            {
                toPos = cleanMove.Contains("O-O-O") ? new Vector2Int(2, backRank) : new Vector2Int(6, backRank);
                pieceType = PieceType.King;
            }
        }
        
        // Validate we have a target position
        if (!toPos.HasValue)
        {
            Debug.LogWarning($"ChatGPT: Could not parse move '{moveNotation}' - invalid target square");
            return false;
        }
        
        // If we don't have a piece yet, try to find or spawn it
        if (pieceToMove == null && pieceType.HasValue)
        {
            // Try to find ANY existing piece of ChatGPT's color (ChatGPT can move any piece)
            // First try to find the specific piece type
            pieceToMove = chessGame.GetPieceByType(pieceType.Value, chatGPTColor);
            
            // If that piece type doesn't exist, get any piece of ChatGPT's color
            if (pieceToMove == null)
            {
                pieceToMove = chessGame.GetRandomPiece(chatGPTColor);
            }
            
            // If still no piece exists, spawn the requested piece type at target location
            if (pieceToMove == null)
            {
                Debug.Log($"ChatGPT: Piece {pieceType.Value} doesn't exist. Spawning it at {chessGame.PositionToSquareName(toPos.Value)}");
                chessGame.SpawnPiece(pieceType.Value, chatGPTColor, toPos.Value);
                pieceToMove = chessGame.GetPieceAt(toPos.Value);
                
                // Piece is already at target, so we're done (just spawned it there)
                // Check for checkmate/stalemate
                if (chessGame.IsCheckmate(chatGPTColor))
                {
                    Debug.Log($"ChatGPT is checkmated after spawning piece!");
                }
                else if (chessGame.IsStalemate(chatGPTColor))
                {
                    Debug.Log($"ChatGPT is stalemated after spawning piece!");
                }
                
                return pieceToMove != null;
            }
        }
        
        // If still no piece, get any piece of ChatGPT's color or spawn a pawn
        if (pieceToMove == null)
        {
            pieceToMove = chessGame.GetRandomPiece(chatGPTColor);
            
            if (pieceToMove == null)
            {
                // No pieces at all, spawn a pawn at a random empty square near the target
                Debug.Log($"ChatGPT: No pieces exist. Spawning pawn at {chessGame.PositionToSquareName(toPos.Value)}");
                chessGame.SpawnPiece(PieceType.Pawn, chatGPTColor, toPos.Value);
                pieceToMove = chessGame.GetPieceAt(toPos.Value);
                
                // Check for checkmate/stalemate
                if (chessGame.IsCheckmate(chatGPTColor))
                {
                    Debug.Log($"ChatGPT is checkmated after spawning piece!");
                }
                else if (chessGame.IsStalemate(chatGPTColor))
                {
                    Debug.Log($"ChatGPT is stalemated after spawning piece!");
                }
                
                return pieceToMove != null;
            }
        }
        
        // Validate we have both a piece and a target
        if (pieceToMove == null || !toPos.HasValue)
        {
            Debug.LogWarning($"ChatGPT: Cannot execute move '{moveNotation}' - missing piece or target position");
            return false;
        }
        
        // CRITICAL: Cannot move enemy pieces - only ChatGPT's own pieces
        if (pieceToMove.color != chatGPTColor)
        {
            Debug.LogWarning($"ChatGPT: Cannot move enemy piece! Move rejected.");
            return false;
        }
        
        // CRITICAL: Cannot capture own king
        ChessPiece targetPiece = chessGame.GetPieceAt(toPos.Value);
        if (targetPiece != null && targetPiece.type == PieceType.King && targetPiece.color == chatGPTColor)
        {
            Debug.LogWarning($"ChatGPT: Cannot capture own king! Move rejected.");
            return false;
        }
        
        // If in check, validate that this move gets out of check
        if (inCheck)
        {
            if (!chessGame.WouldMoveGetOutOfCheck(pieceToMove, toPos.Value))
            {
                Debug.LogWarning($"ChatGPT: Move '{moveNotation}' does not get out of check. Move rejected.");
                return false;
            }
        }
        
        // If playing normally (80% chance), validate the move is legal
        if (!shouldGoNuts)
        {
            List<Vector2Int> validMoves = chessGame.GetValidMovesForPiece(pieceToMove);
            if (!validMoves.Contains(toPos.Value))
            {
                Debug.LogWarning($"ChatGPT: Move '{moveNotation}' is not a legal move (normal mode). Move rejected.");
                return false;
            }
            
            // Execute using normal move (follows chess rules)
            Debug.Log($"ChatGPT: Moving {pieceToMove.color} {pieceToMove.type} from {chessGame.PositionToSquareName(pieceToMove.position)} to {chessGame.PositionToSquareName(toPos.Value)} (NORMAL MODE)");
            chessGame.MovePiece(pieceToMove, toPos.Value);
            chessGame.SetTurn(!chessGame.IsWhiteTurn());
        }
        else
        {
            // Execute using free mode (can break rules, but still can't capture own king)
            Debug.Log($"ChatGPT: Moving {pieceToMove.color} {pieceToMove.type} from {chessGame.PositionToSquareName(pieceToMove.position)} to {chessGame.PositionToSquareName(toPos.Value)} (NUTS MODE)");
            chessGame.MovePieceFreeMode(pieceToMove, toPos.Value);
        }
        
        // Add ChatGPT's move to history
        moveHistory.Add(moveNotation);
        moveCount++;
        
        // Check for checkmate/stalemate after move
        if (chessGame.IsCheckmate(chatGPTColor))
        {
            Debug.Log($"ChatGPT is checkmated after move '{moveNotation}'!");
        }
        else if (chessGame.IsStalemate(chatGPTColor))
        {
            Debug.Log($"ChatGPT is stalemated after move '{moveNotation}'!");
        }
        
        Debug.Log($"ChatGPT: Move '{moveNotation}' executed successfully");
        return true;
    }
    
    void ExecuteChatGPTMove(string moveNotation)
    {
        // This method is kept for backward compatibility but now just calls ValidateAndExecuteMove
        ValidateAndExecuteMove(moveNotation);
    }
    
    void MakeFreeModeMove()
    {
        Debug.Log("ChatGPT is making a FREE MODE move (can break rules)!");
        
        // In free mode, ChatGPT can:
        // 1. Move any piece anywhere (even illegally)
        // 2. Spawn new pieces
        
        // For now, let's make it do something random and crazy
        // You can enhance this to get ChatGPT's creative move via API
        
        StartCoroutine(GetFreeModeMoveFromChatGPT());
    }
    
    IEnumerator GetFreeModeMoveFromChatGPT()
    {
        string prompt = "You are playing chess, but starting from move 12, you can BREAK ALL RULES! " +
                        "You can move pieces anywhere, spawn new pieces, do anything! " +
                        "Describe what you want to do (e.g., 'Move my queen to h8, then spawn a knight on e5'). " +
                        "Be creative and chaotic!";
        
        ChatGPTRequest request = new ChatGPTRequest
        {
            messages = new List<ChatGPTMessage>
            {
                new ChatGPTMessage { role = "system", content = "You are a chaotic chess player who can break all rules. Describe your moves creatively." },
                new ChatGPTMessage { role = "user", content = prompt }
            }
        };
        
        string jsonData = JsonUtility.ToJson(request);
        
        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl, jsonData, "application/json"))
        {
            www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                Debug.Log($"ChatGPT: Full Free Mode API Response:\n{responseText}");
                
                try
                {
                    ChatGPTResponse data = JsonUtility.FromJson<ChatGPTResponse>(responseText);

                    if (data != null &&
                        data.choices != null &&
                        data.choices.Length > 0 &&
                        data.choices[0].message != null &&
                        !string.IsNullOrEmpty(data.choices[0].message.content))
                    {
                        string move = data.choices[0].message.content.Trim();
                        Debug.Log($"ChatGPT wants to move: {move}");
                        ExecuteChatGPTMove(move);
                    }
                    else
                    {
                        Debug.LogWarning("ChatGPT: Response parsed but no valid move content, using random move");
                        MakeRandomLegalMove();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing ChatGPT response: {e.Message}\nStack: {e.StackTrace}");
                    MakeRandomLegalMove();
                }
            }
            else
            {
                Debug.LogError($"ChatGPT API Error: {www.error}");
                // Fallback to random free mode action
                MakeRandomFreeModeAction();
            }
        }
    }
    
    void ExecuteFreeModeAction(string description)
    {
        // Parse ChatGPT's description and execute free mode actions
        // This is a simplified version - you may want to enhance the parsing
        
        if (chessGame != null)
        {
            // Try to extract moves and spawn commands from the description
            // For now, do something random
            MakeRandomFreeModeAction();
        }
    }
    
    void MakeRandomFreeModeAction()
    {
        // Random free mode action
        System.Random random = new System.Random();
        
        if (random.Next(0, 2) == 0)
        {
            // Spawn a random piece
            SpawnRandomPiece();
        }
        else
        {
            // Move a random piece to a random location
            MoveRandomPieceAnywhere();
        }
    }
    
    void SpawnRandomPiece()
    {
        if (chessGame != null)
        {
            // Get a random empty square
            Vector2Int randomSquare = GetRandomEmptySquare();
            if (randomSquare.x >= 0)
            {
                // Spawn a random piece type
                PieceType[] types = { PieceType.Queen, PieceType.Rook, PieceType.Knight, PieceType.Bishop };
                PieceType randomType = types[UnityEngine.Random.Range(0, types.Length)];
                
                chessGame.SpawnPiece(randomType, chatGPTColor, randomSquare);
                Debug.Log($"ChatGPT spawned a {randomType} at {randomSquare}!");
            }
        }
    }
    
    void MoveRandomPieceAnywhere()
    {
        if (chessGame != null)
        {
            // Get a random piece of ChatGPT's color
            ChessPiece randomPiece = chessGame.GetRandomPiece(chatGPTColor);
            if (randomPiece != null)
            {
                // Move it to a random square (even if illegal)
                Vector2Int randomSquare = new Vector2Int(
                    UnityEngine.Random.Range(0, 8),
                    UnityEngine.Random.Range(0, 8)
                );
                
                chessGame.MovePieceFreeMode(randomPiece, randomSquare);
                Debug.Log($"ChatGPT moved {randomPiece.type} to {randomSquare} (free mode)!");
            }
        }
    }
    
    Vector2Int GetRandomEmptySquare()
    {
        for (int i = 0; i < 100; i++) // Try up to 100 times
        {
            Vector2Int square = new Vector2Int(
                UnityEngine.Random.Range(0, 8),
                UnityEngine.Random.Range(0, 8)
            );
            
            if (chessGame != null && chessGame.IsSquareEmpty(square))
            {
                return square;
            }
        }
        return new Vector2Int(-1, -1); // No empty square found
    }
    
    void MakeRandomLegalMove()
    {
        Debug.Log("ChatGPT: Making random legal move (fallback)");
        
        if (chessGame == null)
        {
            Debug.LogError("ChatGPT: chessGame is null! Cannot make random move.");
            return;
        }
        
        bool inCheck = chessGame.IsKingInCheck(chatGPTColor);
        const int MAX_ATTEMPTS = 1000; // Maximum attempts to find a legal move
        int attempts = 0;
        
        // Try to find a legal move
        while (attempts < MAX_ATTEMPTS)
        {
            attempts++;
            
            // Get a random piece
            ChessPiece randomPiece = chessGame.GetRandomPiece(chatGPTColor);
            if (randomPiece == null)
            {
                // No pieces exist, spawn one and move it
                Vector2Int randomSquare = new Vector2Int(
                    UnityEngine.Random.Range(0, 8),
                    UnityEngine.Random.Range(0, 8)
                );
                PieceType[] types = { PieceType.Pawn, PieceType.Queen, PieceType.Rook, PieceType.Knight, PieceType.Bishop };
                PieceType randomType = types[UnityEngine.Random.Range(0, types.Length)];
                chessGame.SpawnPiece(randomType, chatGPTColor, randomSquare);
                randomPiece = chessGame.GetPieceAt(randomSquare);
                if (randomPiece != null)
                {
                    Debug.Log($"ChatGPT: Spawned {randomType} at {randomSquare} as fallback");
                    moveHistory.Add($"{randomType}{chessGame.PositionToSquareName(randomSquare)}");
                    moveCount++;
                    return; // Successfully spawned a piece
                }
                continue;
            }
            
            // If in check, try to find a move that gets out of check
            if (inCheck)
            {
                List<Vector2Int> validMoves = chessGame.GetValidMovesForPiece(randomPiece);
                if (validMoves.Count > 0)
                {
                    Vector2Int randomMove = validMoves[UnityEngine.Random.Range(0, validMoves.Count)];
                    Vector2Int fromPos = randomPiece.position;
                    Debug.Log($"ChatGPT: Moving {randomPiece.type} to {randomMove} (gets out of check)");
                    chessGame.MovePiece(randomPiece, randomMove);
                    chessGame.SetTurn(!chessGame.IsWhiteTurn());
                    moveHistory.Add($"{chessGame.PositionToSquareName(fromPos)}{chessGame.PositionToSquareName(randomMove)}");
                    moveCount++;
                    return; // Successfully made a move
                }
            }
            else
            {
                // Not in check, try to find any valid move first
                List<Vector2Int> validMoves = chessGame.GetValidMovesForPiece(randomPiece);
                if (validMoves.Count > 0)
                {
                    Vector2Int randomMove = validMoves[UnityEngine.Random.Range(0, validMoves.Count)];
                    Vector2Int fromPos = randomPiece.position;
                    Debug.Log($"ChatGPT: Moving {randomPiece.type} to {randomMove}");
                    chessGame.MovePiece(randomPiece, randomMove);
                    chessGame.SetTurn(!chessGame.IsWhiteTurn());
                    moveHistory.Add($"{chessGame.PositionToSquareName(fromPos)}{chessGame.PositionToSquareName(randomMove)}");
                    moveCount++;
                    return; // Successfully made a move
                }
            }
            
            // If no valid moves, try free mode - move any piece anywhere (but respect limits)
            Vector2Int randomTarget = new Vector2Int(
                UnityEngine.Random.Range(0, 8),
                UnityEngine.Random.Range(0, 8)
            );
            
            // CRITICAL: Cannot capture own king
            ChessPiece targetPiece = chessGame.GetPieceAt(randomTarget);
            if (targetPiece != null && targetPiece.type == PieceType.King && targetPiece.color == chatGPTColor)
            {
                continue; // Try again, can't capture own king
            }
            
            // If in check, validate the move gets out of check
            if (inCheck)
            {
                if (chessGame.WouldMoveGetOutOfCheck(randomPiece, randomTarget))
                {
                    Vector2Int fromPos = randomPiece.position;
                    Debug.Log($"ChatGPT: Moving {randomPiece.type} to {randomTarget} (free mode, gets out of check)");
                    chessGame.MovePieceFreeMode(randomPiece, randomTarget);
                    moveHistory.Add($"{chessGame.PositionToSquareName(fromPos)}{chessGame.PositionToSquareName(randomTarget)}");
                    moveCount++;
                    return; // Successfully made a move
                }
            }
            else
            {
                // Not in check, just move it
                Vector2Int fromPos = randomPiece.position;
                Debug.Log($"ChatGPT: Moving {randomPiece.type} to {randomTarget} (free mode)");
                chessGame.MovePieceFreeMode(randomPiece, randomTarget);
                moveHistory.Add($"{chessGame.PositionToSquareName(fromPos)}{chessGame.PositionToSquareName(randomTarget)}");
                moveCount++;
                return; // Successfully made a move
            }
        }
        
        // If we've exhausted all attempts, try one final desperate move
        Debug.LogWarning($"ChatGPT: Could not find a legal move after {MAX_ATTEMPTS} attempts. Making desperate move.");
        ChessPiece lastResortPiece = chessGame.GetRandomPiece(chatGPTColor);
        if (lastResortPiece == null)
        {
            // Spawn a piece as last resort
            Vector2Int lastResortSquare = new Vector2Int(UnityEngine.Random.Range(0, 8), UnityEngine.Random.Range(0, 8));
            chessGame.SpawnPiece(PieceType.Pawn, chatGPTColor, lastResortSquare);
            Debug.Log($"ChatGPT: Spawned pawn at {lastResortSquare} as last resort");
            moveHistory.Add($"P{chessGame.PositionToSquareName(lastResortSquare)}");
            moveCount++;
        }
        else
        {
            Vector2Int fromPos = lastResortPiece.position;
            Vector2Int lastResortTarget = new Vector2Int(UnityEngine.Random.Range(0, 8), UnityEngine.Random.Range(0, 8));
            Debug.Log($"ChatGPT: Moving {lastResortPiece.type} to {lastResortTarget} as last resort");
            chessGame.MovePieceFreeMode(lastResortPiece, lastResortTarget);
            moveHistory.Add($"{chessGame.PositionToSquareName(fromPos)}{chessGame.PositionToSquareName(lastResortTarget)}");
            moveCount++;
        }
    }
    
    public bool IsFreeMode()
    {
        return isFreeMode;
    }
}

