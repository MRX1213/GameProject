using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PieceType
{
    None,
    Pawn,
    Rook,
    Knight,
    Bishop,
    Queen,
    King
}

public enum PieceColor
{
    White,
    Black
}

[System.Serializable]
public class ChessPiece
{
    public PieceType type;
    public PieceColor color;
    public GameObject pieceObject;
    public Vector2Int position;
    public bool hasMoved = false; // Track if piece has moved (for castling and pawn double move)

    public ChessPiece(PieceType t, PieceColor c, Vector2Int pos)
    {
        type = t;
        color = c;
        position = pos;
        hasMoved = false;
    }
}

public class ChessBoardWithPieces : MonoBehaviour
{
    [Header("Board Settings")]
    public int boardSize = 8;
    public float squareSize = 1f;
    public LayerMask pieceLayer;
    [Header("Board Map")]
    public GameObject boardMap; // The parent object containing all 64 sphere markers
    
    private ChessPiece[,] board;
    private ChessPiece selectedPiece = null;
    private bool isWhiteTurn = true;
    private Camera mainCamera;
    private List<Vector2Int> validMoves = new List<Vector2Int>();
    
    // Dictionary mapping square names (e.g., "a1", "b2") to board positions
    private Dictionary<string, Vector2Int> squareNameToPosition = new Dictionary<string, Vector2Int>();
    // Dictionary mapping board positions to sphere GameObjects
    private Dictionary<Vector2Int, GameObject> positionToSphere = new Dictionary<Vector2Int, GameObject>();
    // Dictionary mapping sphere GameObjects to board positions (for quick lookup)
    private Dictionary<GameObject, Vector2Int> sphereToPosition = new Dictionary<GameObject, Vector2Int>();
    
    // En passant tracking: stores the position where a pawn just moved two squares (can be captured en passant)
    private Vector2Int? enPassantTarget = null;
    // Track which color created the en passant opportunity (so we know when to clear it)
    private PieceColor? enPassantColor = null;
    
    // Game state
    private bool gameOver = false;
    private string gameOverMessage = "";
    private PieceColor playerColor = PieceColor.White; // Human player's color
    private ChatGPTChessPlayer chatGPTPlayer = null; // Reference to ChatGPT player
    private int totalMoveCount = 0; // Track total moves for free mode

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();
        
        InitializeBoardMap();
        InitializeBoard();
    }

    void Update()
    {
        HandleInput();
    }

    void InitializeBoardMap()
    {
        if (boardMap == null)
        {
            Debug.LogError("BoardMap object not assigned! Please assign the BoardMap GameObject in the inspector.");
            return;
        }

        // Find all child spheres in the BoardMap
        Transform[] sphereTransforms = boardMap.GetComponentsInChildren<Transform>();
        
        Debug.Log($"Found {sphereTransforms.Length} transforms in BoardMap");
        
        foreach (Transform sphereTransform in sphereTransforms)
        {
            // Skip the parent object itself
            if (sphereTransform == boardMap.transform)
                continue;

            string squareName = sphereTransform.gameObject.name.ToLower();
            Vector2Int boardPos = SquareNameToPosition(squareName);
            
            if (IsValidPosition(boardPos))
            {
                squareNameToPosition[squareName] = boardPos;
                positionToSphere[boardPos] = sphereTransform.gameObject;
                sphereToPosition[sphereTransform.gameObject] = boardPos;
                
                // Ensure sphere has a collider
                if (sphereTransform.GetComponent<Collider>() == null)
                {
                    Debug.LogWarning($"Sphere {squareName} doesn't have a Collider! Adding SphereCollider...");
                    SphereCollider collider = sphereTransform.gameObject.AddComponent<SphereCollider>();
                    collider.radius = 0.5f; // Adjust as needed
                }
                
                // Hide all spheres by default
                Renderer renderer = sphereTransform.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
                // Also hide any child renderers
                Renderer[] childRenderers = sphereTransform.GetComponentsInChildren<Renderer>();
                foreach (var childRenderer in childRenderers)
                {
                    if (childRenderer.transform != sphereTransform)
                    {
                        childRenderer.enabled = false;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Invalid square name: {squareName} on GameObject {sphereTransform.gameObject.name}");
            }
        }

        Debug.Log($"Initialized {positionToSphere.Count} board positions");
        if (positionToSphere.Count != 64)
        {
            Debug.LogWarning($"Expected 64 board positions, but found {positionToSphere.Count}. Make sure all spheres are named correctly (e.g., 'a1', 'b2', etc.)");
        }
    }

    void InitializeBoard()
    {
        board = new ChessPiece[boardSize, boardSize];
        
        // Initialize empty board
        for (int i = 0; i < boardSize; i++)
        {
            for (int j = 0; j < boardSize; j++)
            {
                board[i, j] = null;
            }
        }
        
        // Find and register pieces, snapping them to nearest sphere positions
        FindAndRegisterPieces();
    }

    void FindAndRegisterPieces()
    {
        // Find all chess pieces in the scene
        ChessPieceComponent[] pieces = FindObjectsOfType<ChessPieceComponent>();
        Debug.Log($"Found {pieces.Length} chess pieces in scene");
        
        foreach (var pieceComponent in pieces)
        {
            // Ensure piece has a collider
            if (pieceComponent.GetComponent<Collider>() == null)
            {
                Debug.LogWarning($"Piece {pieceComponent.gameObject.name} doesn't have a Collider! Adding BoxCollider...");
                BoxCollider collider = pieceComponent.gameObject.AddComponent<BoxCollider>();
            }
            
            // Find the nearest sphere position
            Vector2Int boardPos = FindNearestSpherePosition(pieceComponent.transform.position);
            
            if (IsValidPosition(boardPos))
            {
                ChessPiece piece = new ChessPiece(
                    pieceComponent.pieceType,
                    pieceComponent.pieceColor,
                    boardPos
                );
                piece.pieceObject = pieceComponent.gameObject;
                board[boardPos.x, boardPos.y] = piece;
                
                // Snap piece to the sphere position
                SnapPieceToPosition(piece, boardPos);
                
                Debug.Log($"Registered {pieceComponent.pieceColor} {pieceComponent.pieceType} at {PositionToSquareName(boardPos)}");
            }
            else
            {
                Debug.LogWarning($"Could not find valid position for piece {pieceComponent.gameObject.name}");
            }
        }
    }

    void HandleInput()
    {
        // Don't process input if game is over
        if (gameOver)
        {
            return;
        }
        
        // Handle both mouse and touch input
        bool inputPressed = false;
        Vector2 inputPosition = Vector2.zero;

        if (Input.GetMouseButtonDown(0))
        {
            inputPressed = true;
            inputPosition = Input.mousePosition;
        }
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            inputPressed = true;
            inputPosition = Input.GetTouch(0).position;
        }

        if (inputPressed)
        {
            Ray ray = mainCamera.ScreenPointToRay(inputPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);

            if (hits.Length > 0)
            {
                // Sort hits by distance (closest first)
                System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

                foreach (var hit in hits)
                {
                    // Check if we clicked on a piece
                    ChessPieceComponent pieceComponent = hit.collider.GetComponent<ChessPieceComponent>();
                    if (pieceComponent != null)
                    {
                        // Clicked on a piece - find which square it's on
                        Vector2Int boardPos = FindNearestSpherePosition(pieceComponent.transform.position);
                        Debug.Log($"Clicked on piece at position: {boardPos}");
                        HandleSquareClick(boardPos, hit.collider.gameObject);
                        return;
                    }
                    
                    // Check if we clicked on a board sphere
                    if (sphereToPosition.ContainsKey(hit.collider.gameObject))
                    {
                        Vector2Int boardPos = sphereToPosition[hit.collider.gameObject];
                        Debug.Log($"Clicked on board sphere at position: {boardPos}");
                        HandleSquareClick(boardPos, null);
                        return;
                    }
                }
            }
            else
            {
                Debug.Log("No colliders hit by raycast");
            }
        }
    }

    void HandleSquareClick(Vector2Int boardPos, GameObject clickedObject)
    {
        if (!IsValidPosition(boardPos))
        {
            Debug.LogWarning($"Invalid board position: {boardPos}");
            return;
        }

        ChessPiece pieceAtPosition = GetPieceAt(boardPos);

        // Only allow player to interact when it's their turn
        PieceColor currentTurnColor = isWhiteTurn ? PieceColor.White : PieceColor.Black;
        if (currentTurnColor != playerColor)
        {
            Debug.Log($"It's {(currentTurnColor == PieceColor.White ? "White" : "Black")}'s turn (ChatGPT), not yours.");
            return;
        }
        
        if (selectedPiece == null)
        {
            // Select a piece
            if (pieceAtPosition != null && pieceAtPosition.color == playerColor)
            {
                // Clear any existing highlights first
                ClearHighlights();
                selectedPiece = pieceAtPosition;
                validMoves = GetValidMoves(selectedPiece);
                Debug.Log($"Selected {selectedPiece.color} {selectedPiece.type} at {PositionToSquareName(boardPos)}. Valid moves: {validMoves.Count}");
                HighlightSelectedPiece(selectedPiece);
                HighlightValidMoves(validMoves);
            }
            else if (pieceAtPosition != null)
            {
                Debug.Log($"Cannot select {pieceAtPosition.color} piece - it's your opponent's piece");
            }
        }
        else
        {
            // Try to move selected piece
            if (validMoves.Contains(boardPos))
            {
                ChessPiece movedPiece = selectedPiece; // Store reference before clearing
                Vector2Int targetPos = boardPos;
                
                Debug.Log($"Moving {movedPiece.color} {movedPiece.type} to {PositionToSquareName(targetPos)}");
                ClearPieceHighlight(selectedPiece);
                MovePiece(movedPiece, targetPos);
                selectedPiece = null;
                validMoves.Clear();
                ClearHighlights();
                
                // Notify ChatGPT player if human made the move
                if (movedPiece.color == playerColor && chatGPTPlayer != null)
                {
                    string moveNotation = GetMoveNotation(movedPiece, targetPos);
                    chatGPTPlayer.OnPlayerMoveMade(moveNotation);
                    totalMoveCount++;
                }
                
                // Check for checkmate or stalemate after the move
                PieceColor nextPlayerColor = isWhiteTurn ? PieceColor.Black : PieceColor.White;
                CheckGameOver(nextPlayerColor);
                
                isWhiteTurn = !isWhiteTurn;
                Debug.Log($"Turn changed to: {(isWhiteTurn ? "White" : "Black")}");
            }
            else if (pieceAtPosition != null && pieceAtPosition.color == selectedPiece.color)
            {
                // Select a different piece of the same color
                // Clear previous highlights first
                ClearPieceHighlight(selectedPiece);
                ClearHighlights();
                selectedPiece = pieceAtPosition;
                validMoves = GetValidMoves(selectedPiece);
                Debug.Log($"Selected different {selectedPiece.color} {selectedPiece.type} at {PositionToSquareName(boardPos)}");
                HighlightSelectedPiece(selectedPiece);
                HighlightValidMoves(validMoves);
            }
            else
            {
                // Deselect
                Debug.Log("Deselecting piece");
                ClearPieceHighlight(selectedPiece);
                selectedPiece = null;
                validMoves.Clear();
                ClearHighlights();
            }
        }
    }

    public void MovePiece(ChessPiece piece, Vector2Int targetPos)
    {
        Vector2Int oldPos = piece.position;
        
        // Handle castling
        if (piece.type == PieceType.King && !piece.hasMoved)
        {
            int backRank = piece.color == PieceColor.White ? 0 : 7;
            
            // Kingside castling
            if (targetPos.x == 6 && targetPos.y == backRank)
            {
                // Move the rook
                ChessPiece rook = GetPieceAt(new Vector2Int(7, backRank));
                if (rook != null && rook.type == PieceType.Rook)
                {
                    board[7, backRank] = null;
                    rook.position = new Vector2Int(5, backRank);
                    board[5, backRank] = rook;
                    rook.hasMoved = true;
                    SnapPieceToPosition(rook, new Vector2Int(5, backRank));
                }
            }
            // Queenside castling
            else if (targetPos.x == 2 && targetPos.y == backRank)
            {
                // Move the rook
                ChessPiece rook = GetPieceAt(new Vector2Int(0, backRank));
                if (rook != null && rook.type == PieceType.Rook)
                {
                    board[0, backRank] = null;
                    rook.position = new Vector2Int(3, backRank);
                    board[3, backRank] = rook;
                    rook.hasMoved = true;
                    SnapPieceToPosition(rook, new Vector2Int(3, backRank));
                }
            }
        }
        
        // Clear en passant target if it's the turn of the player who created it (opportunity expired)
        if (enPassantTarget.HasValue && enPassantColor.HasValue && piece.color == enPassantColor.Value)
        {
            enPassantTarget = null;
            enPassantColor = null;
        }
        
        // Handle en passant capture
        bool enPassantCapture = false;
        if (piece.type == PieceType.Pawn && enPassantTarget.HasValue && targetPos == enPassantTarget.Value)
        {
            // This is an en passant capture - the captured pawn is one row behind the target
            int direction = piece.color == PieceColor.White ? 1 : -1;
            Vector2Int capturedPawnPos = new Vector2Int(targetPos.x, targetPos.y - direction);
            
            ChessPiece capturedPawn = GetPieceAt(capturedPawnPos);
            if (capturedPawn != null && capturedPawn.type == PieceType.Pawn && capturedPawn.pieceObject != null)
            {
                Destroy(capturedPawn.pieceObject);
                board[capturedPawnPos.x, capturedPawnPos.y] = null;
                enPassantCapture = true;
                enPassantTarget = null; // Clear after using it
                enPassantColor = null;
                Debug.Log($"En passant capture! Captured pawn at {PositionToSquareName(capturedPawnPos)}");
            }
        }
        
        // Remove piece from old position
        board[piece.position.x, piece.position.y] = null;

        // Capture piece at target if exists (and not en passant)
        if (!enPassantCapture)
        {
            ChessPiece capturedPiece = GetPieceAt(targetPos);
            if (capturedPiece != null && capturedPiece.pieceObject != null)
            {
                Destroy(capturedPiece.pieceObject);
            }
        }

        // Set en passant target if pawn moves two squares
        if (piece.type == PieceType.Pawn && Mathf.Abs(targetPos.y - oldPos.y) == 2)
        {
            // The en passant target is the square the pawn passed through (between start and end)
            int direction = piece.color == PieceColor.White ? 1 : -1;
            enPassantTarget = new Vector2Int(targetPos.x, oldPos.y + direction);
            enPassantColor = piece.color; // Remember which color created this opportunity
            Debug.Log($"En passant opportunity created at {PositionToSquareName(enPassantTarget.Value)} by {piece.color}");
        }

        // Move piece to new position
        piece.position = targetPos;
        board[targetPos.x, targetPos.y] = piece;
        piece.hasMoved = true;

        // Snap piece to the sphere position
        SnapPieceToPosition(piece, targetPos);
    }

    void SnapPieceToPosition(ChessPiece piece, Vector2Int boardPos)
    {
        if (piece.pieceObject != null && positionToSphere.ContainsKey(boardPos))
        {
            GameObject targetSphere = positionToSphere[boardPos];
            Vector3 spherePosition = targetSphere.transform.position;
            
            // Add Y offset for pawns to prevent them from falling through the board
            float yOffset = 0f;
            if (piece.type == PieceType.Pawn)
            {
                yOffset = 1f; // Lift pawns up by 1 unit
            }
            
            // Position the piece at the sphere's position with offset
            piece.pieceObject.transform.position = spherePosition + new Vector3(0, yOffset, 0);
        }
    }

    List<Vector2Int> GetValidMoves(ChessPiece piece)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        switch (piece.type)
        {
            case PieceType.Pawn:
                moves.AddRange(GetPawnMoves(piece));
                break;
            case PieceType.Rook:
                moves.AddRange(GetRookMoves(piece));
                break;
            case PieceType.Knight:
                moves.AddRange(GetKnightMoves(piece));
                break;
            case PieceType.Bishop:
                moves.AddRange(GetBishopMoves(piece));
                break;
            case PieceType.Queen:
                moves.AddRange(GetQueenMoves(piece));
                break;
            case PieceType.King:
                moves.AddRange(GetKingMoves(piece));
                break;
        }

        // Filter out moves that would leave the king in check
        List<Vector2Int> validMoves = new List<Vector2Int>();
        bool kingInCheck = IsKingInCheck(piece.color);
        
        foreach (var move in moves)
        {
            // Simulate the move and check if it's legal
            if (IsMoveLegal(piece, move, kingInCheck))
            {
                validMoves.Add(move);
            }
        }

        return validMoves;
    }
    
    bool IsMoveLegal(ChessPiece piece, Vector2Int targetPos, bool kingCurrentlyInCheck)
    {
        // Simulate the move and check if it's legal
        Vector2Int oldPos = piece.position;
        ChessPiece capturedPiece = GetPieceAt(targetPos);
        bool wasMoved = piece.hasMoved;
        
        // Handle special cases that need board state
        ChessPiece capturedEnPassant = null;
        ChessPiece castlingRook = null;
        Vector2Int? rookOldPos = null;
        Vector2Int? rookNewPos = null;
        Vector2Int? oldEnPassantTarget = enPassantTarget;
        PieceColor? oldEnPassantColor = enPassantColor;
        
        // Handle castling simulation
        if (piece.type == PieceType.King && !piece.hasMoved)
        {
            int backRank = piece.color == PieceColor.White ? 0 : 7;
            if (targetPos.x == 6 && targetPos.y == backRank) // Kingside
            {
                castlingRook = GetPieceAt(new Vector2Int(7, backRank));
                if (castlingRook != null)
                {
                    rookOldPos = new Vector2Int(7, backRank);
                    rookNewPos = new Vector2Int(5, backRank);
                }
            }
            else if (targetPos.x == 2 && targetPos.y == backRank) // Queenside
            {
                castlingRook = GetPieceAt(new Vector2Int(0, backRank));
                if (castlingRook != null)
                {
                    rookOldPos = new Vector2Int(0, backRank);
                    rookNewPos = new Vector2Int(3, backRank);
                }
            }
        }
        
        // Temporarily make the move
        board[oldPos.x, oldPos.y] = null;
        board[targetPos.x, targetPos.y] = piece;
        piece.position = targetPos;
        
        // Handle castling - move rook temporarily
        if (castlingRook != null && rookOldPos.HasValue && rookNewPos.HasValue)
        {
            board[rookOldPos.Value.x, rookOldPos.Value.y] = null;
            board[rookNewPos.Value.x, rookNewPos.Value.y] = castlingRook;
            castlingRook.position = rookNewPos.Value;
        }
        
        // Handle en passant capture simulation
        if (piece.type == PieceType.Pawn && enPassantTarget.HasValue && targetPos == enPassantTarget.Value)
        {
            int direction = piece.color == PieceColor.White ? 1 : -1;
            Vector2Int capturedPawnPos = new Vector2Int(targetPos.x, targetPos.y - direction);
            capturedEnPassant = GetPieceAt(capturedPawnPos);
            if (capturedEnPassant != null)
            {
                board[capturedPawnPos.x, capturedPawnPos.y] = null;
            }
        }
        
        // Check if king is in check after the move
        bool kingInCheckAfterMove = IsKingInCheck(piece.color);
        
        // Restore the board state
        piece.position = oldPos;
        board[oldPos.x, oldPos.y] = piece;
        board[targetPos.x, targetPos.y] = capturedPiece;
        piece.hasMoved = wasMoved;
        enPassantTarget = oldEnPassantTarget;
        enPassantColor = oldEnPassantColor;
        
        // Restore castling rook if needed
        if (castlingRook != null && rookOldPos.HasValue && rookNewPos.HasValue)
        {
            board[rookNewPos.Value.x, rookNewPos.Value.y] = null;
            board[rookOldPos.Value.x, rookOldPos.Value.y] = castlingRook;
            castlingRook.position = rookOldPos.Value;
        }
        
        // Restore en passant captured piece if needed
        if (capturedEnPassant != null)
        {
            int direction = piece.color == PieceColor.White ? 1 : -1;
            Vector2Int capturedPawnPos = new Vector2Int(targetPos.x, targetPos.y - direction);
            board[capturedPawnPos.x, capturedPawnPos.y] = capturedEnPassant;
        }
        
        // Move is legal if king is not in check after the move
        // If king was in check, this move must resolve it
        // If king was not in check, this move must not put it in check
        return !kingInCheckAfterMove;
    }

    List<Vector2Int> GetPawnMoves(ChessPiece piece)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        int direction = piece.color == PieceColor.White ? 1 : -1;
        int startRow = piece.color == PieceColor.White ? 1 : 6;

        // Move forward one square
        Vector2Int forward = new Vector2Int(piece.position.x, piece.position.y + direction);
        if (IsValidPosition(forward) && GetPieceAt(forward) == null)
        {
            moves.Add(forward);

            // Move forward two squares from starting position (only if hasn't moved)
            if (piece.position.y == startRow && !piece.hasMoved)
            {
                Vector2Int doubleForward = new Vector2Int(piece.position.x, piece.position.y + (2 * direction));
                if (IsValidPosition(doubleForward) && GetPieceAt(doubleForward) == null)
                {
                    moves.Add(doubleForward);
                }
            }
        }

        // Capture diagonally
        Vector2Int[] captureMoves = new Vector2Int[]
        {
            new Vector2Int(piece.position.x - 1, piece.position.y + direction),
            new Vector2Int(piece.position.x + 1, piece.position.y + direction)
        };

        foreach (var move in captureMoves)
        {
            if (IsValidPosition(move))
            {
                ChessPiece targetPiece = GetPieceAt(move);
                if (targetPiece != null && targetPiece.color != piece.color)
                {
                    moves.Add(move);
                }
            }
        }

        // En passant capture
        if (enPassantTarget.HasValue)
        {
            Vector2Int epPos = enPassantTarget.Value;
            // En passant target is the square the enemy pawn passed through
            // The capturing pawn must be:
            // 1. On an adjacent file (x differs by 1)
            // 2. One rank ahead of the en passant target (in the direction of movement)
            //    For White (moving up, y increases): pawn.y == epPos.y + 1
            //    For Black (moving down, y decreases): pawn.y == epPos.y - 1
            bool isAdjacentFile = Mathf.Abs(piece.position.x - epPos.x) == 1;
            bool isCorrectRank = (piece.position.y == epPos.y + direction);
            
            if (isAdjacentFile && isCorrectRank)
            {
                // The capture square is the en passant target itself (the square the enemy pawn passed through)
                moves.Add(epPos);
                Debug.Log($"En passant move available for {piece.color} pawn at {PositionToSquareName(piece.position)} to {PositionToSquareName(epPos)}");
            }
            else
            {
                // Debug why en passant isn't available
                Debug.Log($"En passant check: {piece.color} pawn at {PositionToSquareName(piece.position)}, epTarget at {PositionToSquareName(epPos)}, direction: {direction}, x diff: {Mathf.Abs(piece.position.x - epPos.x)}, adjacent: {isAdjacentFile}, rank check: pawn.y({piece.position.y}) == epPos.y({epPos.y}) + direction({direction}) = {epPos.y + direction}, match: {isCorrectRank}");
            }
        }

        return moves;
    }

    List<Vector2Int> GetRookMoves(ChessPiece piece)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),  // Up
            new Vector2Int(0, -1), // Down
            new Vector2Int(1, 0),  // Right
            new Vector2Int(-1, 0)  // Left
        };

        foreach (var dir in directions)
        {
            for (int i = 1; i < boardSize; i++)
            {
                Vector2Int newPos = piece.position + dir * i;
                if (!IsValidPosition(newPos)) break;

                ChessPiece targetPiece = GetPieceAt(newPos);
                if (targetPiece == null)
                {
                    moves.Add(newPos);
                }
                else
                {
                    if (targetPiece.color != piece.color)
                        moves.Add(newPos);
                    break;
                }
            }
        }

        return moves;
    }

    List<Vector2Int> GetKnightMoves(ChessPiece piece)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        Vector2Int[] knightMoves = new Vector2Int[]
        {
            new Vector2Int(2, 1), new Vector2Int(2, -1),
            new Vector2Int(-2, 1), new Vector2Int(-2, -1),
            new Vector2Int(1, 2), new Vector2Int(1, -2),
            new Vector2Int(-1, 2), new Vector2Int(-1, -2)
        };

        foreach (var move in knightMoves)
        {
            Vector2Int newPos = piece.position + move;
            if (IsValidPosition(newPos))
            {
                ChessPiece targetPiece = GetPieceAt(newPos);
                if (targetPiece == null || targetPiece.color != piece.color)
                {
                    moves.Add(newPos);
                }
            }
        }

        return moves;
    }

    List<Vector2Int> GetBishopMoves(ChessPiece piece)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 1),   // Up-Right
            new Vector2Int(1, -1),  // Down-Right
            new Vector2Int(-1, 1),  // Up-Left
            new Vector2Int(-1, -1)  // Down-Left
        };

        foreach (var dir in directions)
        {
            for (int i = 1; i < boardSize; i++)
            {
                Vector2Int newPos = piece.position + dir * i;
                if (!IsValidPosition(newPos)) break;

                ChessPiece targetPiece = GetPieceAt(newPos);
                if (targetPiece == null)
                {
                    moves.Add(newPos);
                }
                else
                {
                    if (targetPiece.color != piece.color)
                        moves.Add(newPos);
                    break;
                }
            }
        }

        return moves;
    }

    List<Vector2Int> GetQueenMoves(ChessPiece piece)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        moves.AddRange(GetRookMoves(piece));
        moves.AddRange(GetBishopMoves(piece));
        return moves;
    }

    List<Vector2Int> GetKingMoves(ChessPiece piece)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        Vector2Int[] kingMoves = new Vector2Int[]
        {
            new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        PieceColor enemyColor = piece.color == PieceColor.White ? PieceColor.Black : PieceColor.White;

        foreach (var move in kingMoves)
        {
            Vector2Int newPos = piece.position + move;
            if (IsValidPosition(newPos))
            {
                ChessPiece targetPiece = GetPieceAt(newPos);
                if (targetPiece == null || targetPiece.color != piece.color)
                {
                    // King cannot move to a square that is attacked
                    if (!IsSquareAttacked(newPos, enemyColor))
                    {
                        moves.Add(newPos);
                    }
                }
            }
        }

        // Castling
        if (!piece.hasMoved && !IsKingInCheck(piece.color))
        {
            int backRank = piece.color == PieceColor.White ? 0 : 7;
            
            // Kingside castling (right side)
            if (CanCastle(piece, true))
            {
                moves.Add(new Vector2Int(6, backRank)); // King moves to g1/g8
            }
            
            // Queenside castling (left side)
            if (CanCastle(piece, false))
            {
                moves.Add(new Vector2Int(2, backRank)); // King moves to c1/c8
            }
        }

        return moves;
    }

    bool CanCastle(ChessPiece king, bool kingside)
    {
        int backRank = king.color == PieceColor.White ? 0 : 7;
        int rookX = kingside ? 7 : 0;
        
        // Check if rook exists and hasn't moved
        ChessPiece rook = GetPieceAt(new Vector2Int(rookX, backRank));
        if (rook == null || rook.type != PieceType.Rook || rook.color != king.color || rook.hasMoved)
        {
            return false;
        }
        
        // Check if squares between king and rook are empty
        int startX = kingside ? 5 : 1;
        int endX = kingside ? 6 : 3;
        
        for (int x = startX; x <= endX; x++)
        {
            if (GetPieceAt(new Vector2Int(x, backRank)) != null)
            {
                return false;
            }
        }
        
        // Check if king would pass through or land on attacked squares
        // For kingside: check squares f and g (5, 6)
        // For queenside: check squares b, c, d (1, 2, 3)
        int checkStartX = kingside ? 4 : 2; // King's current position or one square toward rook
        int checkEndX = kingside ? 6 : 2;   // Final castling position
        
        for (int x = checkStartX; x <= checkEndX; x++)
        {
            if (IsSquareAttacked(new Vector2Int(x, backRank), king.color == PieceColor.White ? PieceColor.Black : PieceColor.White))
            {
                return false;
            }
        }
        
        return true;
    }

    bool IsSquareAttacked(Vector2Int square, PieceColor byColor)
    {
        // Check if any piece of the attacking color can move to this square
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                ChessPiece piece = GetPieceAt(new Vector2Int(x, y));
                if (piece != null && piece.color == byColor)
                {
                    // Get all possible moves for this piece (without checking if king would be in check)
                    List<Vector2Int> pieceMoves = GetPieceMovesWithoutCheckValidation(piece);
                    if (pieceMoves.Contains(square))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    bool IsKingInCheck(PieceColor kingColor)
    {
        // Find the king
        ChessPiece king = null;
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                ChessPiece piece = GetPieceAt(new Vector2Int(x, y));
                if (piece != null && piece.type == PieceType.King && piece.color == kingColor)
                {
                    king = piece;
                    break;
                }
            }
            if (king != null) break;
        }
        
        if (king == null) return false;
        
        return IsSquareAttacked(king.position, kingColor == PieceColor.White ? PieceColor.Black : PieceColor.White);
    }
    
    bool HasLegalMoves(PieceColor playerColor)
    {
        // Check if the player has any legal moves
        int totalMoves = 0;
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                ChessPiece piece = GetPieceAt(new Vector2Int(x, y));
                if (piece != null && piece.color == playerColor)
                {
                    List<Vector2Int> moves = GetValidMoves(piece);
                    totalMoves += moves.Count;
                    if (moves.Count > 0)
                    {
                        return true; // Found at least one legal move
                    }
                }
            }
        }
        Debug.Log($"{playerColor} has {totalMoves} total legal moves");
        return false; // No legal moves found
    }
    
    bool IsCheckmate(PieceColor kingColor)
    {
        // Checkmate occurs when:
        // 1. King is in check
        // 2. King has no legal moves
        if (!IsKingInCheck(kingColor))
        {
            return false;
        }
        
        return !HasLegalMoves(kingColor);
    }
    
    bool IsStalemate(PieceColor playerColor)
    {
        // Stalemate occurs when:
        // 1. King is NOT in check
        // 2. Player has no legal moves (neither king nor any other piece can move)
        bool inCheck = IsKingInCheck(playerColor);
        bool hasMoves = HasLegalMoves(playerColor);
        
        if (inCheck)
        {
            return false; // If in check, it's checkmate, not stalemate
        }
        
        if (!hasMoves)
        {
            Debug.Log($"Stalemate detected! {playerColor} is not in check but has no legal moves.");
            return true;
        }
        
        return false;
    }
    
    void CheckGameOver(PieceColor currentPlayerColor)
    {
        if (gameOver) return; // Already game over
        
        if (IsCheckmate(currentPlayerColor))
        {
            gameOver = true;
            PieceColor winner = currentPlayerColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            gameOverMessage = $"{winner} wins by checkmate!";
            Debug.Log(gameOverMessage);
            
            // Clear any selected piece and highlights
            if (selectedPiece != null)
            {
                ClearPieceHighlight(selectedPiece);
                selectedPiece = null;
            }
            ClearHighlights();
            
            // You can add UI display here
            // For example: ShowGameOverUI(gameOverMessage);
        }
        else if (IsStalemate(currentPlayerColor))
        {
            gameOver = true;
            gameOverMessage = "Stalemate! The game is a draw.";
            Debug.Log(gameOverMessage);
            
            // Clear any selected piece and highlights
            if (selectedPiece != null)
            {
                ClearPieceHighlight(selectedPiece);
                selectedPiece = null;
            }
            ClearHighlights();
            
            // You can add UI display here
            // For example: ShowGameOverUI(gameOverMessage);
        }
    }
    
    // Public methods to check game state
    public bool IsGameOver()
    {
        return gameOver;
    }
    
    public string GetGameOverMessage()
    {
        return gameOverMessage;
    }
    
    public void ResetGame()
    {
        gameOver = false;
        gameOverMessage = "";
        isWhiteTurn = true;
        selectedPiece = null;
        validMoves.Clear();
        totalMoveCount = 0;
        ClearHighlights();
        // Note: You'll need to reset the board state and piece positions manually
        // or reload the scene/prefab
    }
    
    // ChatGPT Integration Methods
    public void SetPlayerColor(PieceColor color)
    {
        playerColor = color;
        isWhiteTurn = (color == PieceColor.White);
    }
    
    public void SetChatGPTPlayer(ChatGPTChessPlayer player)
    {
        chatGPTPlayer = player;
    }
    
    public void ExecuteMoveFromNotation(string notation, PieceColor movingColor)
    {
        // Parse algebraic notation (simplified - you may want to enhance this)
        // Format examples: "e2e4", "Nf3", "O-O", "e4"
        
        if (notation.Contains("O-O"))
        {
            // Castling
            int backRank = movingColor == PieceColor.White ? 0 : 7;
            ChessPiece king = GetKing(movingColor);
            if (king != null)
            {
                Vector2Int targetPos = notation.Contains("O-O-O") ? new Vector2Int(2, backRank) : new Vector2Int(6, backRank);
                MovePiece(king, targetPos);
                isWhiteTurn = !isWhiteTurn;
            }
        }
        else
        {
            // Try to parse as "from-to" notation (e.g., "e2e4")
            if (notation.Length >= 4)
            {
                string from = notation.Substring(0, 2);
                string to = notation.Substring(2, 2);
                Vector2Int fromPos = SquareNameToPosition(from);
                Vector2Int toPos = SquareNameToPosition(to);
                
                ChessPiece piece = GetPieceAt(fromPos);
                if (piece != null && piece.color == movingColor)
                {
                    MovePiece(piece, toPos);
                    isWhiteTurn = !isWhiteTurn;
                }
            }
        }
    }
    
    public void MovePieceFreeMode(ChessPiece piece, Vector2Int targetPos)
    {
        // Free mode move - no validation, can be illegal
        Vector2Int oldPos = piece.position;
        
        // Remove piece from old position
        board[oldPos.x, oldPos.y] = null;
        
        // Capture piece at target if exists
        ChessPiece capturedPiece = GetPieceAt(targetPos);
        if (capturedPiece != null && capturedPiece.pieceObject != null)
        {
            Destroy(capturedPiece.pieceObject);
        }
        
        // Move piece to new position
        piece.position = targetPos;
        board[targetPos.x, targetPos.y] = piece;
        piece.hasMoved = true;
        
        // Snap piece to position
        SnapPieceToPosition(piece, targetPos);
        
        isWhiteTurn = !isWhiteTurn;
        totalMoveCount++;
    }
    
    public void SpawnPiece(PieceType type, PieceColor color, Vector2Int position)
    {
        if (!IsValidPosition(position))
            return;
        
        // Check if square is empty (or remove existing piece)
        ChessPiece existingPiece = GetPieceAt(position);
        if (existingPiece != null && existingPiece.pieceObject != null)
        {
            Destroy(existingPiece.pieceObject);
        }
        
        // Create a new piece GameObject (you'll need to have piece prefabs)
        // For now, we'll create a placeholder
        GameObject piecePrefab = GetPiecePrefab(type, color);
        if (piecePrefab != null)
        {
            GameObject newPieceObj = Instantiate(piecePrefab);
            ChessPieceComponent pieceComponent = newPieceObj.GetComponent<ChessPieceComponent>();
            if (pieceComponent == null)
            {
                pieceComponent = newPieceObj.AddComponent<ChessPieceComponent>();
            }
            pieceComponent.pieceType = type;
            pieceComponent.pieceColor = color;
            
            // Create ChessPiece
            ChessPiece newPiece = new ChessPiece(type, color, position);
            newPiece.pieceObject = newPieceObj;
            newPiece.hasMoved = true; // Spawned pieces are considered "moved"
            
            board[position.x, position.y] = newPiece;
            SnapPieceToPosition(newPiece, position);
            
            Debug.Log($"Spawned {color} {type} at {PositionToSquareName(position)}");
        }
    }
    
    GameObject GetPiecePrefab(PieceType type, PieceColor color)
    {
        // You'll need to set up piece prefabs in your project
        // For now, return null - you should create a prefab system
        // Example: return piecePrefabs[type][color];
        return null; // TODO: Implement piece prefab system
    }
    
    public ChessPiece GetRandomPiece(PieceColor color)
    {
        List<ChessPiece> pieces = new List<ChessPiece>();
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                ChessPiece piece = GetPieceAt(new Vector2Int(x, y));
                if (piece != null && piece.color == color)
                {
                    pieces.Add(piece);
                }
            }
        }
        
        if (pieces.Count > 0)
        {
            return pieces[UnityEngine.Random.Range(0, pieces.Count)];
        }
        return null;
    }
    
    public bool IsSquareEmpty(Vector2Int position)
    {
        return GetPieceAt(position) == null;
    }
    
    public List<Vector2Int> GetValidMovesForPiece(ChessPiece piece)
    {
        return GetValidMoves(piece);
    }
    
    ChessPiece GetKing(PieceColor color)
    {
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                ChessPiece piece = GetPieceAt(new Vector2Int(x, y));
                if (piece != null && piece.type == PieceType.King && piece.color == color)
                {
                    return piece;
                }
            }
        }
        return null;
    }
    
    string GetMoveNotation(ChessPiece piece, Vector2Int targetPos)
    {
        // Simple notation: from-to (e.g., "e2e4")
        return PositionToSquareName(piece.position) + PositionToSquareName(targetPos);
    }

    List<Vector2Int> GetPieceMovesWithoutCheckValidation(ChessPiece piece)
    {
        // Get moves for a piece without checking if it puts own king in check
        // This is used for checking if squares are attacked
        switch (piece.type)
        {
            case PieceType.Pawn:
                return GetPawnMoves(piece);
            case PieceType.Rook:
                return GetRookMoves(piece);
            case PieceType.Knight:
                return GetKnightMoves(piece);
            case PieceType.Bishop:
                return GetBishopMoves(piece);
            case PieceType.Queen:
                return GetQueenMoves(piece);
            case PieceType.King:
                // For king, we need to get moves without castling to avoid recursion
                List<Vector2Int> moves = new List<Vector2Int>();
                Vector2Int[] kingMoves = new Vector2Int[]
                {
                    new Vector2Int(0, 1), new Vector2Int(0, -1),
                    new Vector2Int(1, 0), new Vector2Int(-1, 0),
                    new Vector2Int(1, 1), new Vector2Int(1, -1),
                    new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                };
                foreach (var move in kingMoves)
                {
                    Vector2Int newPos = piece.position + move;
                    if (IsValidPosition(newPos))
                    {
                        ChessPiece targetPiece = GetPieceAt(newPos);
                        if (targetPiece == null || targetPiece.color != piece.color)
                        {
                            moves.Add(newPos);
                        }
                    }
                }
                return moves;
            default:
                return new List<Vector2Int>();
        }
    }

    ChessPiece GetPieceAt(Vector2Int position)
    {
        if (IsValidPosition(position))
            return board[position.x, position.y];
        return null;
    }

    bool IsValidPosition(Vector2Int position)
    {
        return position.x >= 0 && position.x < boardSize &&
               position.y >= 0 && position.y < boardSize;
    }

    Vector2Int FindNearestSpherePosition(Vector3 worldPos)
    {
        // Find the nearest sphere to the given world position
        float minDistance = float.MaxValue;
        Vector2Int nearestPos = new Vector2Int(-1, -1);

        foreach (var kvp in positionToSphere)
        {
            float distance = Vector3.Distance(worldPos, kvp.Value.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPos = kvp.Key;
            }
        }

        return nearestPos;
    }

    Vector2Int SquareNameToPosition(string squareName)
    {
        // Convert chess notation (e.g., "a1", "b2", "h8") to Vector2Int
        // Files (columns): a-h map to 0-7
        // Ranks (rows): 1-8 map to 0-7 (1 is bottom, 8 is top)
        
        if (string.IsNullOrEmpty(squareName) || squareName.Length < 2)
            return new Vector2Int(-1, -1);

        char file = squareName[0]; // a-h
        if (!char.IsDigit(squareName[1]))
            return new Vector2Int(-1, -1);

        int rank = int.Parse(squareName.Substring(1)); // 1-8

        int x = file - 'a'; // a=0, b=1, ..., h=7
        int y = rank - 1;   // 1=0, 2=1, ..., 8=7

        return new Vector2Int(x, y);
    }

    string PositionToSquareName(Vector2Int position)
    {
        // Convert Vector2Int to chess notation
        char file = (char)('a' + position.x);
        int rank = position.y + 1;
        return $"{file}{rank}";
    }

    void HighlightValidMoves(List<Vector2Int> moves)
    {
        // Show spheres for valid moves
        foreach (var move in moves)
        {
            if (positionToSphere.ContainsKey(move))
            {
                GameObject sphere = positionToSphere[move];
                // Enable the sphere renderer
                Renderer renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
                // Also enable any child renderers
                Renderer[] childRenderers = sphere.GetComponentsInChildren<Renderer>();
                foreach (var childRenderer in childRenderers)
                {
                    childRenderer.enabled = true;
                }
            }
        }
    }

    void ClearHighlights()
    {
        // Hide all board spheres
        foreach (var kvp in positionToSphere)
        {
            GameObject sphere = kvp.Value;
            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
            // Also disable any child renderers
            Renderer[] childRenderers = sphere.GetComponentsInChildren<Renderer>();
            foreach (var childRenderer in childRenderers)
            {
                childRenderer.enabled = false;
            }
        }
    }
    
    void HighlightSelectedPiece(ChessPiece piece)
    {
        if (piece != null && piece.pieceObject != null)
        {
            // Try to find PieceOutline component first (our custom component)
            PieceOutline outline = piece.pieceObject.GetComponent<PieceOutline>();
            if (outline == null)
            {
                // Also check in children
                outline = piece.pieceObject.GetComponentInChildren<PieceOutline>();
            }
            
            if (outline != null)
            {
                Debug.Log($"Found PieceOutline on {piece.pieceObject.name}, enabling outline");
                outline.SetOutlineEnabled(true);
                outline.enabled = true;
            }
            else
            {
                Debug.LogWarning($"No PieceOutline component found on {piece.pieceObject.name}. Please add PieceOutline component to enable highlighting.");
                
                // Try to find and enable other outline/halo components
                MonoBehaviour[] components = piece.pieceObject.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    // Try to enable outline using reflection (works with most outline systems)
                    var enabledProperty = component.GetType().GetProperty("enabled");
                    if (enabledProperty != null && 
                        (component.GetType().Name.ToLower().Contains("outline") || 
                         component.GetType().Name.ToLower().Contains("halo")))
                    {
                        try
                        {
                            enabledProperty.SetValue(component, true);
                        }
                        catch { }
                    }
                }
            }
            
            // Also try to enable any child objects that might be outline/halo
            Transform[] children = piece.pieceObject.GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                if (child != piece.pieceObject.transform && 
                    (child.name.ToLower().Contains("outline") || 
                     child.name.ToLower().Contains("halo") ||
                     child.name.ToLower().Contains("highlight")))
                {
                    child.gameObject.SetActive(true);
                    Renderer renderer = child.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
            }
        }
    }
    
    void ClearPieceHighlight(ChessPiece piece)
    {
        if (piece != null && piece.pieceObject != null)
        {
            // Try to find PieceOutline component first
            PieceOutline outline = piece.pieceObject.GetComponent<PieceOutline>();
            if (outline == null)
            {
                // Also check in children
                outline = piece.pieceObject.GetComponentInChildren<PieceOutline>();
            }
            
            if (outline != null)
            {
                outline.SetOutlineEnabled(false);
                outline.enabled = false;
            }
            else
            {
                // Disable other outline/halo components
                MonoBehaviour[] components = piece.pieceObject.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var enabledProperty = component.GetType().GetProperty("enabled");
                    if (enabledProperty != null && 
                        (component.GetType().Name.ToLower().Contains("outline") || 
                         component.GetType().Name.ToLower().Contains("halo")))
                    {
                        try
                        {
                            enabledProperty.SetValue(component, false);
                        }
                        catch { }
                    }
                }
            }
            
            // Disable any child outline/halo objects
            Transform[] children = piece.pieceObject.GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                if (child != piece.pieceObject.transform && 
                    (child.name.ToLower().Contains("outline") || 
                     child.name.ToLower().Contains("halo") ||
                     child.name.ToLower().Contains("highlight")))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }
}
