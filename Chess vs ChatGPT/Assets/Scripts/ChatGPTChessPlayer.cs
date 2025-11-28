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
    public string model = "gpt-4";
    public List<ChatGPTMessage> messages;
    public float temperature = 0.7f;
}

[System.Serializable]
public class ChatGPTResponse
{
    public ChatGPTChoice[] choices;
}

[System.Serializable]
public class ChatGPTChoice
{
    public ChatGPTMessage message;
}

public class ChatGPTChessPlayer : MonoBehaviour
{
    [Header("API Settings")]
    public string apiKey = "API_KEY_WILL_BE_HERE";
    public string apiUrl = "https://api.openai.com/v1/chat/completions";
    
    [Header("Game References")]
    public ChessBoardWithPieces chessGame;
    
    private PieceColor chatGPTColor;
    private int moveCount = 0;
    private const int FREE_MODE_START_MOVE = 12;
    private bool isFreeMode = false;
    private List<string> moveHistory = new List<string>();
    
    public void Initialize(PieceColor color)
    {
        chatGPTColor = color;
        moveCount = 0;
        isFreeMode = false;
        moveHistory.Clear();
    }
    
    public void SetChessGame(ChessBoardWithPieces game)
    {
        chessGame = game;
    }
    
    public void Reset()
    {
        moveCount = 0;
        isFreeMode = false;
        moveHistory.Clear();
    }
    
    public void OnPlayerMoveMade(string moveNotation)
    {
        moveHistory.Add(moveNotation);
        moveCount++;
        
        // Check if we've reached free mode
        if (moveCount >= FREE_MODE_START_MOVE && !isFreeMode)
        {
            isFreeMode = true;
            Debug.Log("ChatGPT has entered FREE MODE! It can now break chess rules!");
        }
        
        // Wait a bit then make ChatGPT's move
        StartCoroutine(MakeChatGPTMove());
    }
    
    IEnumerator MakeChatGPTMove()
    {
        // Wait a moment before ChatGPT responds
        yield return new WaitForSeconds(1f);
        
        if (isFreeMode)
        {
            // Free mode - ChatGPT can do anything
            MakeFreeModeMove();
        }
        else
        {
            // Normal mode - ChatGPT must follow chess rules
            yield return StartCoroutine(GetChatGPTMove());
        }
    }
    
    IEnumerator GetChatGPTMove()
    {
        // Build the prompt
        string prompt = BuildChessPrompt();
        
        // Create the API request
        ChatGPTRequest request = new ChatGPTRequest
        {
            messages = new List<ChatGPTMessage>
            {
                new ChatGPTMessage { role = "system", content = "You are a chess player. Respond with moves in algebraic notation (e.g., 'e2e4', 'Nf3', 'O-O'). Only respond with the move, nothing else." },
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
                try
                {
                    // Parse JSON response (ChatGPT API format)
                    int choicesIndex = responseText.IndexOf("\"choices\"");
                    if (choicesIndex >= 0)
                    {
                        int contentIndex = responseText.IndexOf("\"content\"", choicesIndex);
                        if (contentIndex >= 0)
                        {
                            int startQuote = responseText.IndexOf("\"", contentIndex + 9) + 1;
                            int endQuote = responseText.IndexOf("\"", startQuote);
                            if (endQuote > startQuote)
                            {
                                string move = responseText.Substring(startQuote, endQuote - startQuote).Trim();
                                Debug.Log($"ChatGPT wants to move: {move}");
                                ExecuteChatGPTMove(move);
                                yield break;
                            }
                        }
                    }
                    // Fallback if parsing fails
                    MakeRandomLegalMove();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing ChatGPT response: {e.Message}");
                    MakeRandomLegalMove();
                }
            }
            else
            {
                Debug.LogError($"ChatGPT API Error: {www.error}");
                // Fallback to random legal move
                MakeRandomLegalMove();
            }
        }
    }
    
    string BuildChessPrompt()
    {
        string prompt = "Current chess position. You are playing as " + chatGPTColor + ". ";
        prompt += "Make your move. ";
        prompt += "Previous moves: " + string.Join(", ", moveHistory) + ". ";
        prompt += "Respond with only the move in algebraic notation (e.g., 'e2e4' or 'Nf3').";
        return prompt;
    }
    
    void ExecuteChatGPTMove(string moveNotation)
    {
        // Parse the move and execute it
        // This is a simplified parser - you may need to enhance it
        if (chessGame != null)
        {
            chessGame.ExecuteMoveFromNotation(moveNotation, chatGPTColor);
        }
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
                try
                {
                    // Parse JSON response (ChatGPT API format)
                    int choicesIndex = responseText.IndexOf("\"choices\"");
                    if (choicesIndex >= 0)
                    {
                        int contentIndex = responseText.IndexOf("\"content\"", choicesIndex);
                        if (contentIndex >= 0)
                        {
                            int startQuote = responseText.IndexOf("\"", contentIndex + 9) + 1;
                            int endQuote = responseText.IndexOf("\"", startQuote);
                            if (endQuote > startQuote)
                            {
                                string description = responseText.Substring(startQuote, endQuote - startQuote).Trim();
                                Debug.Log($"ChatGPT free mode action: {description}");
                                // Parse and execute the free mode actions
                                ExecuteFreeModeAction(description);
                                yield break;
                            }
                        }
                    }
                    // Fallback if parsing fails
                    MakeRandomFreeModeAction();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error parsing ChatGPT response: {e.Message}");
                    MakeRandomFreeModeAction();
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
        // Fallback: make a random legal move
        if (chessGame != null)
        {
            ChessPiece randomPiece = chessGame.GetRandomPiece(chatGPTColor);
            if (randomPiece != null)
            {
                List<Vector2Int> moves = chessGame.GetValidMovesForPiece(randomPiece);
                if (moves.Count > 0)
                {
                    Vector2Int randomMove = moves[UnityEngine.Random.Range(0, moves.Count)];
                    chessGame.MovePiece(randomPiece, randomMove);
                }
            }
        }
    }
    
    public bool IsFreeMode()
    {
        return isFreeMode;
    }
}

