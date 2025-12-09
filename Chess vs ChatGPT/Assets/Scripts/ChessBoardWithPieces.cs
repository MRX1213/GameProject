using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

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
    
    // Store initial piece positions for reset
    private Dictionary<GameObject, Vector2Int> initialPiecePositions = new Dictionary<GameObject, Vector2Int>();
    
    // Store standard starting positions for all pieces
    private Dictionary<Vector2Int, PieceInfo> standardStartingPositions = new Dictionary<Vector2Int, PieceInfo>();
    
    [System.Serializable]
    private struct PieceInfo
    {
        public PieceType type;
        public PieceColor color;
        
        public PieceInfo(PieceType t, PieceColor c)
        {
            type = t;
            color = c;
        }
    }
    
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
    
    // Promotion state
    private ChessPiece pendingPromotionPawn = null; // Pawn waiting for promotion choice
    private System.Action<PieceType> onPromotionSelected = null; // Callback for promotion choice
    private ChatGPTChessPlayer chatGPTPlayer = null; // Reference to ChatGPT player
    private ChessUIManager chessUIManager = null; // Reference to UI manager
    private int totalMoveCount = 0; // Track total moves for free mode

    void Start()
    {
        UpdateMainCamera();
        InitializeBoardMap();
        InitializeBoard();
        InitializeStandardStartingPositions();
    }
    
    void InitializeStandardStartingPositions()
    {
        // White pieces (back rank - rank 1, y=0)
        standardStartingPositions[new Vector2Int(0, 0)] = new PieceInfo(PieceType.Rook, PieceColor.White);
        standardStartingPositions[new Vector2Int(1, 0)] = new PieceInfo(PieceType.Knight, PieceColor.White);
        standardStartingPositions[new Vector2Int(2, 0)] = new PieceInfo(PieceType.Bishop, PieceColor.White);
        standardStartingPositions[new Vector2Int(3, 0)] = new PieceInfo(PieceType.Queen, PieceColor.White);
        standardStartingPositions[new Vector2Int(4, 0)] = new PieceInfo(PieceType.King, PieceColor.White);
        standardStartingPositions[new Vector2Int(5, 0)] = new PieceInfo(PieceType.Bishop, PieceColor.White);
        standardStartingPositions[new Vector2Int(6, 0)] = new PieceInfo(PieceType.Knight, PieceColor.White);
        standardStartingPositions[new Vector2Int(7, 0)] = new PieceInfo(PieceType.Rook, PieceColor.White);
        
        // White pawns (rank 2, y=1)
        for (int x = 0; x < 8; x++)
        {
            standardStartingPositions[new Vector2Int(x, 1)] = new PieceInfo(PieceType.Pawn, PieceColor.White);
        }
        
        // Black pawns (rank 7, y=6)
        for (int x = 0; x < 8; x++)
        {
            standardStartingPositions[new Vector2Int(x, 6)] = new PieceInfo(PieceType.Pawn, PieceColor.Black);
        }
        
        // Black pieces (back rank - rank 8, y=7)
        standardStartingPositions[new Vector2Int(0, 7)] = new PieceInfo(PieceType.Rook, PieceColor.Black);
        standardStartingPositions[new Vector2Int(1, 7)] = new PieceInfo(PieceType.Knight, PieceColor.Black);
        standardStartingPositions[new Vector2Int(2, 7)] = new PieceInfo(PieceType.Bishop, PieceColor.Black);
        standardStartingPositions[new Vector2Int(3, 7)] = new PieceInfo(PieceType.Queen, PieceColor.Black);
        standardStartingPositions[new Vector2Int(4, 7)] = new PieceInfo(PieceType.King, PieceColor.Black);
        standardStartingPositions[new Vector2Int(5, 7)] = new PieceInfo(PieceType.Bishop, PieceColor.Black);
        standardStartingPositions[new Vector2Int(6, 7)] = new PieceInfo(PieceType.Knight, PieceColor.Black);
        standardStartingPositions[new Vector2Int(7, 7)] = new PieceInfo(PieceType.Rook, PieceColor.Black);
        
        Debug.Log($"Initialized {standardStartingPositions.Count} standard starting positions");
    }
    
    void UpdateMainCamera()
    {
        // Find the active main camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            // Try to find any active camera
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in cameras)
            {
                if (cam.gameObject.activeInHierarchy && cam.enabled)
                {
                    mainCamera = cam;
                    break;
                }
            }
        }
        
        if (mainCamera == null)
        {
            Debug.LogWarning("No active camera found! Raycasting will not work.");
        }
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
                
                // Store initial position for reset
                initialPiecePositions[pieceComponent.gameObject] = boardPos;
                
                // Snap piece to the sphere position
                SnapPieceToPosition(piece, boardPos);
                
                // Add label to the piece
                AddPieceLabel(piece);
                
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
        
        // Update camera reference in case it changed
        if (mainCamera == null || !mainCamera.gameObject.activeInHierarchy || !mainCamera.enabled)
        {
            UpdateMainCamera();
        }
        
        // Make sure we have a valid camera
        if (mainCamera == null)
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
            
            Debug.Log($"Raycast from camera: {mainCamera.name}, Screen pos: {inputPosition}, Hits: {hits.Length}");

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

        // Check for pawn promotion
        // Note: For ChatGPT pawns, we promote to Queen by default here, but ChatGPTChessPlayer
        // can override with a different type if specified in the move notation.
        if (piece.type == PieceType.Pawn)
        {
            // White pawns promote on rank 8 (y = 7), black pawns promote on rank 1 (y = 0)
            if ((piece.color == PieceColor.White && targetPos.y == 7) ||
                (piece.color == PieceColor.Black && targetPos.y == 0))
            {
                // If it's the player's pawn, request promotion choice from UI
                if (piece.color == playerColor && chessUIManager != null)
                {
                    pendingPromotionPawn = piece;
                    chessUIManager.ShowPromotionPanel(piece.color, (promotionType) => {
                        PromotePawn(piece, promotionType);
                        pendingPromotionPawn = null;
                    });
                }
                else
                {
                    // For ChatGPT or if no UI manager, default to Queen
                    // ChatGPTChessPlayer can override this if a different type was specified in notation
                    PromotePawn(piece, PieceType.Queen);
                }
            }
        }

        // Snap piece to the sphere position
        SnapPieceToPosition(piece, targetPos);
        
        // Check if either king is missing from the board after the move
        CheckForMissingKings();
    }
    
    void CheckForMissingKings()
    {
        ChessPiece whiteKing = GetKing(PieceColor.White);
        ChessPiece blackKing = GetKing(PieceColor.Black);
        
        if (whiteKing == null)
        {
            gameOver = true;
            gameOverMessage = "Black wins! White king has been captured.";
            Debug.Log(gameOverMessage);
            ClearHighlights();
        }
        else if (blackKing == null)
        {
            gameOver = true;
            gameOverMessage = "White wins! Black king has been captured.";
            Debug.Log(gameOverMessage);
            ClearHighlights();
        }
    }
    
    void PromotePawn(ChessPiece pawn, PieceType promotionType)
    {
        // Allow promotion from Pawn or from Queen (if ChatGPT wants to override default Queen promotion)
        bool isPawn = pawn.type == PieceType.Pawn;
        bool isQueenOverride = pawn.type == PieceType.Queen && promotionType != PieceType.Queen;
        
        if (!isPawn && !isQueenOverride)
        {
            Debug.LogWarning($"Cannot promote {pawn.type} to {promotionType}. Only pawns can be promoted (or Queen can be overridden).");
            return;
        }
        
        string pieceTypeName = isPawn ? "pawn" : "queen";
        Debug.Log($"Promoting {pawn.color} {pieceTypeName} at {PositionToSquareName(pawn.position)} to {promotionType}");
        
        Vector2Int position = pawn.position;
        PieceColor color = pawn.color;
        
        // Try to get the prefab for the new piece type
        GameObject newPiecePrefab = GetPiecePrefab(promotionType, color);
        
        // If no prefab, try to find an existing piece of this type in the scene to use as template
        // This includes inactive decorative pieces on the side of the board
        if (newPiecePrefab == null)
        {
            // Search for active pieces first (includes inactive parameter = false)
            ChessPieceComponent[] activePieces = FindObjectsOfType<ChessPieceComponent>(false);
            Debug.Log($"Searching for {promotionType} template among {activePieces.Length} active pieces in scene");
            
            // First priority: Find active piece of same type AND same color
            foreach (var pieceComponent in activePieces)
            {
                if (pieceComponent.gameObject != pawn.pieceObject && 
                    pieceComponent.pieceType == promotionType && 
                    pieceComponent.pieceColor == color)
                {
                    newPiecePrefab = pieceComponent.gameObject;
                    Debug.Log($"Found existing {color} {promotionType} ({pieceComponent.gameObject.name}) in scene to use as template for promotion");
                    break;
                }
            }
            
            // Second priority: Find active piece of same type but opposite color (we'll update the color component)
            if (newPiecePrefab == null)
            {
                PieceColor oppositeColor = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                foreach (var pieceComponent in activePieces)
                {
                    if (pieceComponent.gameObject != pawn.pieceObject && 
                        pieceComponent.pieceType == promotionType && 
                        pieceComponent.pieceColor == oppositeColor)
                    {
                        newPiecePrefab = pieceComponent.gameObject;
                        Debug.Log($"Found {oppositeColor} {promotionType} template (will change to {color}) to use for promotion");
                        break;
                    }
                }
            }
            
            // Third priority: Search the board array for pieces
            if (newPiecePrefab == null)
            {
                for (int x = 0; x < boardSize; x++)
                {
                    for (int y = 0; y < boardSize; y++)
                    {
                        ChessPiece boardPiece = GetPieceAt(new Vector2Int(x, y));
                        if (boardPiece != null && boardPiece.type == promotionType)
                        {
                            if (boardPiece.pieceObject != null && boardPiece.pieceObject != pawn.pieceObject)
                            {
                                newPiecePrefab = boardPiece.pieceObject;
                                Debug.Log($"Found {boardPiece.color} {promotionType} on board at {PositionToSquareName(new Vector2Int(x, y))} to use as template");
                                break;
                            }
                        }
                    }
                    if (newPiecePrefab != null) break;
                }
            }
            
            // Fourth priority: Search for INACTIVE decorative pieces (includes inactive parameter = true)
            // These are pieces placed on the side of the board as decoration/templates
            if (newPiecePrefab == null)
            {
                ChessPieceComponent[] allPiecesIncludingInactive = FindObjectsOfType<ChessPieceComponent>(true);
                Debug.Log($"Searching for {promotionType} template among {allPiecesIncludingInactive.Length} pieces (including inactive) in scene");
                
                // Prefer inactive decorative pieces of the same color
                foreach (var pieceComponent in allPiecesIncludingInactive)
                {
                    if (pieceComponent.gameObject != pawn.pieceObject && 
                        pieceComponent.pieceType == promotionType && 
                        pieceComponent.pieceColor == color)
                    {
                        // Check if it's inactive (decorative piece on the side)
                        if (!pieceComponent.gameObject.activeInHierarchy)
                        {
                            newPiecePrefab = pieceComponent.gameObject;
                            Debug.Log($"Found inactive decorative {color} {promotionType} ({pieceComponent.gameObject.name}) to use as template for promotion");
                            break;
                        }
                    }
                }
                
                // If still not found, try inactive pieces of opposite color
                if (newPiecePrefab == null)
                {
                    PieceColor oppositeColor = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
                    foreach (var pieceComponent in allPiecesIncludingInactive)
                    {
                        if (pieceComponent.gameObject != pawn.pieceObject && 
                            pieceComponent.pieceType == promotionType && 
                            pieceComponent.pieceColor == oppositeColor)
                        {
                            if (!pieceComponent.gameObject.activeInHierarchy)
                            {
                                newPiecePrefab = pieceComponent.gameObject;
                                Debug.Log($"Found inactive decorative {oppositeColor} {promotionType} template (will change to {color}) to use for promotion");
                                break;
                            }
                        }
                    }
                }
                
                // Last resort: Any inactive piece of this type
                if (newPiecePrefab == null)
                {
                    foreach (var pieceComponent in allPiecesIncludingInactive)
                    {
                        if (pieceComponent.gameObject != pawn.pieceObject && 
                            pieceComponent.pieceType == promotionType &&
                            !pieceComponent.gameObject.activeInHierarchy)
                        {
                            newPiecePrefab = pieceComponent.gameObject;
                            Debug.Log($"Found inactive {pieceComponent.pieceColor} {promotionType} ({pieceComponent.gameObject.name}) anywhere in scene to use as template");
                            break;
                        }
                    }
                }
            }
        }
        
        if (newPiecePrefab != null && pawn.pieceObject != null)
        {
            // Replace the pawn GameObject with the new piece GameObject
            Vector3 oldPosition = pawn.pieceObject.transform.position;
            Quaternion oldRotation = pawn.pieceObject.transform.rotation;
            Transform parent = pawn.pieceObject.transform.parent;
            
            // Destroy the old pawn GameObject
            Destroy(pawn.pieceObject);
            
            // Instantiate the new piece
            GameObject newPieceObj = Instantiate(newPiecePrefab);
            
            // Make sure the new piece is active BEFORE doing anything else
            newPieceObj.SetActive(true);
            
            // Activate all children recursively
            SetActiveRecursive(newPieceObj, true);
            
            newPieceObj.transform.position = oldPosition;
            newPieceObj.transform.rotation = oldRotation;
            if (parent != null)
            {
                newPieceObj.transform.SetParent(parent);
            }
            
            // Add/update ChessPieceComponent
            ChessPieceComponent component = newPieceObj.GetComponent<ChessPieceComponent>();
            if (component == null)
            {
                component = newPieceObj.AddComponent<ChessPieceComponent>();
            }
            component.pieceType = promotionType;
            component.pieceColor = color;
            
            // Update the ChessPiece reference
            pawn.pieceObject = newPieceObj;
            pawn.type = promotionType;
            
            // Add/update label for the promoted piece
            AddPieceLabel(pawn);
            
            // Ensure it has a collider
            if (newPieceObj.GetComponent<Collider>() == null)
            {
                BoxCollider collider = newPieceObj.AddComponent<BoxCollider>();
            }
            
            // Ensure renderers are enabled (including inactive children)
            Renderer[] renderers = newPieceObj.GetComponentsInChildren<Renderer>(true); // Include inactive
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                    // Also ensure the GameObject containing the renderer is active
                    if (renderer.gameObject != null)
                    {
                        renderer.gameObject.SetActive(true);
                    }
                }
            }
            
            // Also enable all MeshRenderers and SkinnedMeshRenderers specifically
            MeshRenderer[] meshRenderers = newPieceObj.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                if (mr != null)
                {
                    mr.enabled = true;
                    if (mr.gameObject != null)
                    {
                        mr.gameObject.SetActive(true);
                    }
                }
            }
            
            SkinnedMeshRenderer[] skinnedRenderers = newPieceObj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinnedRenderers)
            {
                if (smr != null)
                {
                    smr.enabled = true;
                    if (smr.gameObject != null)
                    {
                        smr.gameObject.SetActive(true);
                    }
                }
            }
            
            // Snap to position to ensure it's correctly placed
            SnapPieceToPosition(pawn, position);
            
            Debug.Log($"Pawn replaced with {promotionType} GameObject at {PositionToSquareName(position)}. Active: {newPieceObj.activeSelf}, Renderers: {renderers.Length}");
        }
        else
        {
            // Fallback: Just update the type if we can't replace the GameObject
            Debug.LogWarning($"Cannot replace pawn GameObject - no prefab or template found. Updating type only. You may need to set up piece prefabs or have pieces of each type in the scene.");
            pawn.type = promotionType;
            
            // Update the ChessPieceComponent if it exists
            if (pawn.pieceObject != null)
            {
                ChessPieceComponent component = pawn.pieceObject.GetComponent<ChessPieceComponent>();
                if (component != null)
                {
                    component.pieceType = promotionType;
                }
            }
        }
        
        Debug.Log($"Pawn promoted to {promotionType}");
    }
    
    public void PromotePawnTo(ChessPiece pawn, PieceType promotionType)
    {
        PromotePawn(pawn, promotionType);
    }

    void SnapPieceToPosition(ChessPiece piece, Vector2Int boardPos)
    {
        if (piece.pieceObject != null && positionToSphere.ContainsKey(boardPos))
        {
            GameObject targetSphere = positionToSphere[boardPos];
            if (targetSphere == null || targetSphere.transform == null)
            {
                Debug.LogWarning($"Cannot snap piece - targetSphere is null for position {PositionToSquareName(boardPos)}");
                return;
            }
            
            Vector3 spherePosition = targetSphere.transform.position;
            
            // Validate sphere position to prevent NaN/Infinity
            if (!IsValidVector3(spherePosition))
            {
                Debug.LogWarning($"Invalid sphere position (NaN/Infinity) at {PositionToSquareName(boardPos)}. Using fallback position.");
                spherePosition = Vector3.zero;
            }
            
            // Add Y offset for pawns to prevent them from falling through the board
            float yOffset = 0f;
            if (piece.type == PieceType.Pawn)
            {
                yOffset = 1f; // Lift pawns up by 1 unit
            }
            
            // Position the piece at the sphere's position with offset
            Vector3 targetPosition = spherePosition + new Vector3(0, yOffset, 0);
            
            // Validate target position before setting
            if (IsValidVector3(targetPosition))
            {
                piece.pieceObject.transform.position = targetPosition;
            }
            else
            {
                Debug.LogError($"Invalid target position (NaN/Infinity) for piece at {PositionToSquareName(boardPos)}. Using fallback.");
                piece.pieceObject.transform.position = Vector3.zero + new Vector3(0, yOffset, 0);
            }
            
            // Ensure the piece is active
            if (!piece.pieceObject.activeSelf)
            {
                piece.pieceObject.SetActive(true);
            }
            
            Debug.Log($"Snapped {piece.color} {piece.type} to {PositionToSquareName(boardPos)} at position {targetPosition}");
        }
        else
        {
            if (piece.pieceObject == null)
            {
                Debug.LogWarning($"Cannot snap piece - pieceObject is null for {piece.color} {piece.type} at {PositionToSquareName(boardPos)}");
            }
            else if (!positionToSphere.ContainsKey(boardPos))
            {
                Debug.LogWarning($"Cannot snap piece - no sphere found for position {PositionToSquareName(boardPos)}");
            }
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

    public bool IsKingInCheck(PieceColor kingColor)
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
    
    public bool IsCheckmate(PieceColor kingColor)
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
    
    public bool IsStalemate(PieceColor playerColor)
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
        enPassantTarget = null;
        enPassantColor = null;
        ClearHighlights();
        
        // Clear the board first - remove all pieces from board array
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                board[x, y] = null;
            }
        }
        
        // Get all existing pieces in the scene (including those that might be off-board or in wrong positions)
        ChessPieceComponent[] allPieces = FindObjectsOfType<ChessPieceComponent>();
        List<ChessPieceComponent> availablePieces = new List<ChessPieceComponent>(allPieces);
        
        Debug.Log($"ResetGame: Found {allPieces.Length} pieces in scene before reset");
        
        // Reset piece types if they were promoted (change promoted pieces back to their original type if needed)
        // This handles cases where pawns were promoted to queens, etc.
        foreach (var pieceComponent in allPieces)
        {
            // We'll handle type restoration during placement based on standard positions
        }
        
        // Dictionary to track which pieces we've placed
        Dictionary<Vector2Int, bool> positionsPlaced = new Dictionary<Vector2Int, bool>();
        
        // First pass: Try to match existing pieces to their standard starting positions
        foreach (var kvp in standardStartingPositions)
        {
            Vector2Int pos = kvp.Key;
            PieceInfo requiredPiece = kvp.Value;
            
            // Try to find an existing piece that matches this position
            ChessPieceComponent matchingPiece = null;
            for (int i = availablePieces.Count - 1; i >= 0; i--)
            {
                var piece = availablePieces[i];
                if (piece.pieceType == requiredPiece.type && piece.pieceColor == requiredPiece.color)
                {
                    matchingPiece = piece;
                    availablePieces.RemoveAt(i);
                    break;
                }
            }
            
            if (matchingPiece != null)
            {
                // Place this piece at the standard position
                // Update the piece component to match the required type (in case it was promoted)
                matchingPiece.pieceType = requiredPiece.type;
                matchingPiece.pieceColor = requiredPiece.color;
                
                // Make sure the piece GameObject is active and visible
                matchingPiece.gameObject.SetActive(true);
                
                // Ensure renderers are enabled
                Renderer[] renderers = matchingPiece.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
                
                ChessPiece chessPiece = new ChessPiece(requiredPiece.type, requiredPiece.color, pos);
                chessPiece.pieceObject = matchingPiece.gameObject;
                chessPiece.hasMoved = false;
                board[pos.x, pos.y] = chessPiece;
                SnapPieceToPosition(chessPiece, pos);
                positionsPlaced[pos] = true;
                Debug.Log($"Placed {requiredPiece.color} {requiredPiece.type} at {PositionToSquareName(pos)}");
            }
            else
            {
                // Need to spawn this piece
                positionsPlaced[pos] = false;
                Debug.LogWarning($"Missing {requiredPiece.color} {requiredPiece.type} for position {PositionToSquareName(pos)}");
            }
        }
        
        // Second pass: Spawn missing pieces or find templates to clone
        foreach (var kvp in standardStartingPositions)
        {
            Vector2Int pos = kvp.Key;
            PieceInfo requiredPiece = kvp.Value;
            
            if (!positionsPlaced[pos])
            {
                // Try to spawn the missing piece using prefab
                GameObject piecePrefab = GetPiecePrefab(requiredPiece.type, requiredPiece.color);
                if (piecePrefab != null)
                {
                    SpawnPiece(requiredPiece.type, requiredPiece.color, pos);
                    ChessPiece spawnedPiece = GetPieceAt(pos);
                    if (spawnedPiece != null)
                    {
                        spawnedPiece.hasMoved = false; // Reset hasMoved for spawned pieces
                        positionsPlaced[pos] = true;
                        Debug.Log($"Spawned {requiredPiece.color} {requiredPiece.type} at {PositionToSquareName(pos)}");
                    }
                }
                else
                {
                    // Can't spawn from prefab - try to find any piece of this type/color to use as template
                    ChessPieceComponent templatePiece = null;
                    
                    // First, try to find in available pieces list
                    for (int i = availablePieces.Count - 1; i >= 0; i--)
                    {
                        var piece = availablePieces[i];
                        if (piece.pieceType == requiredPiece.type && piece.pieceColor == requiredPiece.color)
                        {
                            templatePiece = piece;
                            availablePieces.RemoveAt(i);
                            break;
                        }
                    }
                    
                    // If not found in available pieces, search the entire scene (including pieces already placed)
                    if (templatePiece == null)
                    {
                        ChessPieceComponent[] allScenePieces = FindObjectsOfType<ChessPieceComponent>();
                        foreach (var piece in allScenePieces)
                        {
                            if (piece.pieceType == requiredPiece.type && piece.pieceColor == requiredPiece.color)
                            {
                                templatePiece = piece;
                                Debug.Log($"Found template {requiredPiece.color} {requiredPiece.type} in scene: {piece.gameObject.name}");
                                break;
                            }
                        }
                    }
                    
                    if (templatePiece != null)
                    {
                        // Clone the template piece
                        GameObject newPieceObj = Instantiate(templatePiece.gameObject);
                        
                        // Make sure the new piece is active and visible
                        newPieceObj.SetActive(true);
                        
                        ChessPieceComponent newComponent = newPieceObj.GetComponent<ChessPieceComponent>();
                        if (newComponent == null)
                        {
                            newComponent = newPieceObj.AddComponent<ChessPieceComponent>();
                        }
                        newComponent.pieceType = requiredPiece.type;
                        newComponent.pieceColor = requiredPiece.color;
                        
                        // Ensure renderers are enabled
                        Renderer[] renderers = newPieceObj.GetComponentsInChildren<Renderer>();
                        foreach (var renderer in renderers)
                        {
                            if (renderer != null)
                            {
                                renderer.enabled = true;
                            }
                        }
                        
                        // Create ChessPiece
                        ChessPiece chessPiece = new ChessPiece(requiredPiece.type, requiredPiece.color, pos);
                        chessPiece.pieceObject = newPieceObj;
                        chessPiece.hasMoved = false;
                        board[pos.x, pos.y] = chessPiece;
                        SnapPieceToPosition(chessPiece, pos);
                        positionsPlaced[pos] = true;
                        
                        // Add label to the piece
                        AddPieceLabel(chessPiece);
                        
                        // Ensure it has a collider
                        if (newPieceObj.GetComponent<Collider>() == null)
                        {
                            BoxCollider collider = newPieceObj.AddComponent<BoxCollider>();
                        }
                        
                        Debug.Log($"Cloned {requiredPiece.color} {requiredPiece.type} from template and placed at {PositionToSquareName(pos)}");
                    }
                    else
                    {
                        Debug.LogError($"Cannot place {requiredPiece.color} {requiredPiece.type} at {PositionToSquareName(pos)} - no prefab and no template found in scene");
                    }
                }
            }
        }
        
        // Destroy any remaining pieces that weren't placed (extra pieces)
        foreach (var extraPiece in availablePieces)
        {
            if (extraPiece != null && extraPiece.gameObject != null)
            {
                Debug.Log($"Destroying extra piece: {extraPiece.pieceColor} {extraPiece.pieceType}");
                Destroy(extraPiece.gameObject);
            }
        }
        
        // Update initial positions dictionary for future resets
        initialPiecePositions.Clear();
        foreach (var kvp in standardStartingPositions)
        {
            ChessPiece piece = GetPieceAt(kvp.Key);
            if (piece != null && piece.pieceObject != null)
            {
                initialPiecePositions[piece.pieceObject] = kvp.Key;
            }
        }
        
        Debug.Log("Game reset - all pieces placed at standard starting positions");
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
    
    public void SetUIManager(ChessUIManager uiManager)
    {
        chessUIManager = uiManager;
    }
    
    public void UpdateCameraReference()
    {
        UpdateMainCamera();
    }
    
    public void ExecuteMoveFromNotation(string notation, PieceColor movingColor)
    {
        Debug.Log($"Executing move from notation: '{notation}' for {movingColor}");
        
        // Clean up the notation (remove spaces, newlines, etc.)
        notation = notation.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "");
        
        // Parse algebraic notation (simplified - you may want to enhance this)
        // Format examples: "e2e4", "Nf3", "O-O", "e4"
        
        if (notation.Contains("O-O"))
        {
            Debug.Log("Parsing castling move");
            // Castling
            int backRank = movingColor == PieceColor.White ? 0 : 7;
            ChessPiece king = GetKing(movingColor);
            if (king != null)
            {
                Vector2Int targetPos = notation.Contains("O-O-O") ? new Vector2Int(2, backRank) : new Vector2Int(6, backRank);
                MovePiece(king, targetPos);
                isWhiteTurn = !isWhiteTurn;
                Debug.Log($"Castling move executed");
            }
            else
            {
                Debug.LogError("King not found for castling!");
            }
        }
        else
        {
            // Try to parse as "from-to" notation (e.g., "e2e4")
            if (notation.Length >= 4)
            {
                string from = notation.Substring(0, 2).ToLower();
                string to = notation.Substring(2, 2).ToLower();
                Vector2Int fromPos = SquareNameToPosition(from);
                Vector2Int toPos = SquareNameToPosition(to);
                
                Debug.Log($"Parsing move: from {from} ({fromPos}) to {to} ({toPos})");
                
                if (!IsValidPosition(fromPos) || !IsValidPosition(toPos))
                {
                    Debug.LogError($"Invalid positions: from {fromPos} to {toPos}");
                    return;
                }
                
                ChessPiece piece = GetPieceAt(fromPos);
                if (piece != null && piece.color == movingColor)
                {
                    Debug.Log($"Found piece: {piece.type} at {fromPos}, moving to {toPos}");
                    MovePiece(piece, toPos);
                    isWhiteTurn = !isWhiteTurn;
                    Debug.Log($"Move executed, turn changed to: {(isWhiteTurn ? "White" : "Black")}");
                }
                else
                {
                    Debug.LogError($"No piece found at {fromPos} or piece color mismatch. Piece: {piece}, Expected color: {movingColor}");
                }
            }
            else
            {
                Debug.LogWarning($"Move notation too short or invalid: '{notation}' (length: {notation.Length})");
            }
        }
    }
    
    public bool IsWhiteTurn()
    {
        return isWhiteTurn;
    }
    
    public void SetTurn(bool whiteTurn)
    {
        isWhiteTurn = whiteTurn;
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
        
        // Check for pawn promotion
        if (piece.type == PieceType.Pawn)
        {
            // White pawns promote on rank 8 (y = 7), black pawns promote on rank 1 (y = 0)
            if ((piece.color == PieceColor.White && targetPos.y == 7) ||
                (piece.color == PieceColor.Black && targetPos.y == 0))
            {
                PromotePawn(piece, PieceType.Queen); // Default to Queen for free mode
            }
        }
        
        // Snap piece to position
        SnapPieceToPosition(piece, targetPos);
        
        // Check if either king is missing from the board after the move
        CheckForMissingKings();
        if (gameOver) return; // Don't continue if game ended due to missing king
        
        isWhiteTurn = !isWhiteTurn;
        totalMoveCount++;
    }
    
    public bool WouldMovePutKingInCheck(ChessPiece piece, Vector2Int targetPos, PieceColor kingColor)
    {
        // Simulate the move and check if the king would be in check after
        Vector2Int oldPos = piece.position;
        ChessPiece capturedPiece = GetPieceAt(targetPos);
        
        // Temporarily make the move
        board[oldPos.x, oldPos.y] = null;
        board[targetPos.x, targetPos.y] = piece;
        piece.position = targetPos;
        
        // Check if king is in check
        bool kingInCheck = IsKingInCheck(kingColor);
        
        // Restore the board
        board[oldPos.x, oldPos.y] = piece;
        board[targetPos.x, targetPos.y] = capturedPiece;
        piece.position = oldPos;
        
        return kingInCheck;
    }
    
    public bool WouldMoveGetOutOfCheck(ChessPiece piece, Vector2Int targetPos)
    {
        // Simulate the move and check if it gets out of check
        Vector2Int oldPos = piece.position;
        ChessPiece capturedPiece = GetPieceAt(targetPos);
        
        // Temporarily make the move
        board[oldPos.x, oldPos.y] = null;
        board[targetPos.x, targetPos.y] = piece;
        piece.position = targetPos;
        
        // Check if still in check
        bool stillInCheck = IsKingInCheck(piece.color);
        
        // Restore the board state
        piece.position = oldPos;
        board[oldPos.x, oldPos.y] = piece;
        board[targetPos.x, targetPos.y] = capturedPiece;
        
        return !stillInCheck;
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
            
            // Add label to the spawned piece
            AddPieceLabel(newPiece);
            
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
    
    public ChessPiece GetKing(PieceColor color)
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
    
    public ChessPiece GetPieceByType(PieceType type, PieceColor color)
    {
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                ChessPiece piece = GetPieceAt(new Vector2Int(x, y));
                if (piece != null && piece.type == type && piece.color == color)
                {
                    return piece;
                }
            }
        }
        return null;
    }
    
    public bool HasPieceOfType(PieceType type, PieceColor color)
    {
        return GetPieceByType(type, color) != null;
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

    public ChessPiece GetPieceAt(Vector2Int position)
    {
        if (IsValidPosition(position))
            return board[position.x, position.y];
        return null;
    }

    public bool IsValidPosition(Vector2Int position)
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

    public Vector2Int SquareNameToPosition(string squareName)
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

    public string PositionToSquareName(Vector2Int position)
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
    
    // Helper method to recursively set active state on GameObject and all children
    void SetActiveRecursive(GameObject obj, bool active)
    {
        if (obj == null) return;
        
        obj.SetActive(active);
        
        // Recursively activate all children
        foreach (Transform child in obj.transform)
        {
            SetActiveRecursive(child.gameObject, active);
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
    
    /// <summary>
    /// Adds a text label above a chess piece that displays the piece name and always faces the camera
    /// </summary>
    void AddPieceLabel(ChessPiece piece)
    {
        if (piece == null || piece.pieceObject == null)
            return;
        
        // Check if label already exists
        Transform existingLabel = piece.pieceObject.transform.Find("PieceLabel");
        if (existingLabel != null)
        {
            // Update existing label text, size, and color
            BillboardText billboard = existingLabel.GetComponent<BillboardText>();
            if (billboard != null)
            {
                billboard.SetText(GetPieceDisplayName(piece.type));
            }
            
            // Update TextMeshPro if it exists
            Component existingTmp = null;
            Type existingTmpType = Type.GetType("TMPro.TextMeshPro, Assembly-CSharp");
            if (existingTmpType != null)
            {
                existingTmp = existingLabel.GetComponent(existingTmpType);
            }
            if (existingTmp != null)
            {
                Type existingTmpType2 = existingTmp.GetType();
                var fontSizeProperty = existingTmpType2.GetProperty("fontSize");
                var colorProperty = existingTmpType2.GetProperty("color");
                if (fontSizeProperty != null) fontSizeProperty.SetValue(existingTmp, 0.8f); // 20% smaller (1f * 0.8 = 0.8f)
                if (colorProperty != null) colorProperty.SetValue(existingTmp, Color.white);
            }
            else
            {
                // Update TextMesh if it exists
                TextMesh textMesh = existingLabel.GetComponent<TextMesh>();
                if (textMesh != null)
                {
                    textMesh.fontSize = 26; // 20% smaller (33 * 0.8 ≈ 26)
                    textMesh.color = Color.white;
                    textMesh.characterSize = 0.134f; // 20% smaller (0.167 * 0.8 ≈ 0.134)
                }
            }
            
            return;
        }
        
        // Create a new GameObject for the label
        GameObject labelObj = new GameObject("PieceLabel");
        labelObj.transform.SetParent(piece.pieceObject.transform);
        labelObj.transform.localPosition = Vector3.zero;
        
        // Try to get the bounds of the piece to position the label above it
        Renderer pieceRenderer = piece.pieceObject.GetComponentInChildren<Renderer>();
        if (pieceRenderer != null)
        {
            Bounds bounds = pieceRenderer.bounds;
            // Validate bounds to prevent NaN/Infinity
            if (IsValidVector3(bounds.center) && IsValidVector3(bounds.extents))
            {
                // Position label above the piece (in world space, then convert to local)
                Vector3 worldPos = bounds.center + Vector3.up * (bounds.extents.y + 0.3f);
                if (IsValidVector3(worldPos))
                {
                    labelObj.transform.position = worldPos;
                }
                else
                {
                    // Fallback: position 1 unit above the piece
                    labelObj.transform.localPosition = Vector3.up * 1.5f;
                }
            }
            else
            {
                // Fallback: position 1 unit above the piece
                labelObj.transform.localPosition = Vector3.up * 1.5f;
            }
        }
        else
        {
            // Fallback: position 1 unit above the piece
            labelObj.transform.localPosition = Vector3.up * 1.5f;
        }
        
        // Try to add TextMeshPro first (preferred), then fallback to TextMesh
        Component tmp = null;
        try
        {
            // Try to add TextMeshPro using reflection
            Type tmpType = Type.GetType("TMPro.TextMeshPro, Assembly-CSharp");
            if (tmpType != null)
            {
                tmp = labelObj.AddComponent(tmpType) as Component;
                if (tmp != null)
                {
                    // Set properties using reflection
                    var textProperty = tmpType.GetProperty("text");
                    var fontSizeProperty = tmpType.GetProperty("fontSize");
                    var alignmentProperty = tmpType.GetProperty("alignment");
                    var colorProperty = tmpType.GetProperty("color");
                    var sortingOrderProperty = tmpType.GetProperty("sortingOrder");
                    var enableWordWrappingProperty = tmpType.GetProperty("enableWordWrapping");
                    var overflowModeProperty = tmpType.GetProperty("overflowMode");
                    
                    if (textProperty != null) textProperty.SetValue(tmp, GetPieceDisplayName(piece.type));
                    if (fontSizeProperty != null) fontSizeProperty.SetValue(tmp, 0.8f); // 20% smaller (1f * 0.8 = 0.8f)
                    if (colorProperty != null) colorProperty.SetValue(tmp, Color.white); // Always white
                    if (sortingOrderProperty != null) sortingOrderProperty.SetValue(tmp, 100);
                    
                    // Set alignment enum value
                    if (alignmentProperty != null)
                    {
                        Type alignmentType = Type.GetType("TMPro.TextAlignmentOptions, Assembly-CSharp");
                        if (alignmentType != null)
                        {
                            var centerValue = Enum.Parse(alignmentType, "Center");
                            alignmentProperty.SetValue(tmp, centerValue);
                        }
                    }
                    
                    if (enableWordWrappingProperty != null) enableWordWrappingProperty.SetValue(tmp, false);
                    if (overflowModeProperty != null)
                    {
                        Type overflowType = Type.GetType("TMPro.TextOverflowModes, Assembly-CSharp");
                        if (overflowType != null)
                        {
                            var overflowValue = Enum.Parse(overflowType, "Overflow");
                            overflowModeProperty.SetValue(tmp, overflowValue);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log($"Could not add TextMeshPro: {e.Message}. Falling back to TextMesh.");
        }
        
        // Fallback to legacy TextMesh if TextMeshPro is not available
        if (tmp == null)
        {
            TextMesh textMesh = labelObj.AddComponent<TextMesh>();
            textMesh.text = GetPieceDisplayName(piece.type);
            textMesh.fontSize = 40; // 20% smaller (50 * 0.8 = 40)
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.white; // Always white
            textMesh.characterSize = 0.24f; // 20% smaller (0.3 * 0.8 = 0.24)
        }
        
        // Add BillboardText component to make it face the camera
        BillboardText billboardText = labelObj.AddComponent<BillboardText>();
        
        Debug.Log($"Added label '{GetPieceDisplayName(piece.type)}' to {piece.color} {piece.type}");
    }
    
    /// <summary>
    /// Creates a grey transparent background with a black rounded border for the label
    /// </summary>
    void AddLabelBackground(GameObject labelObj)
    {
        // Get the actual text bounds
        float textWidth = 0.4f;
        float textHeight = 0.2f;
        
        // Try to get TextMeshPro bounds
        Component tmp = null;
        Type tmpType = Type.GetType("TMPro.TextMeshPro, Assembly-CSharp");
        if (tmpType != null)
        {
            tmp = labelObj.GetComponent(tmpType);
        }
        
        if (tmp != null)
        {
            // Force TextMeshPro to update its mesh to get accurate bounds
            var forceMeshUpdateMethod = tmp.GetType().GetMethod("ForceMeshUpdate");
            if (forceMeshUpdateMethod != null)
            {
                forceMeshUpdateMethod.Invoke(tmp, new object[] { false });
            }
            
            // Try to get preferred width/height first (more accurate for text size)
            var preferredWidthProperty = tmp.GetType().GetProperty("preferredWidth");
            var preferredHeightProperty = tmp.GetType().GetProperty("preferredHeight");
            
            if (preferredWidthProperty != null && preferredHeightProperty != null)
            {
                float prefWidth = (float)(preferredWidthProperty.GetValue(tmp) ?? 0f);
                float prefHeight = (float)(preferredHeightProperty.GetValue(tmp) ?? 0f);
                
                if (prefWidth > 0 && prefHeight > 0)
                {
                    // TextMeshPro preferred dimensions are in local units
                    // Get the rectTransform to convert properly
                    RectTransform rectTransform = labelObj.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        textWidth = prefWidth;
                        textHeight = prefHeight;
                    }
                    else
                    {
                        // For 3D TextMeshPro, use a scale factor
                        // Typically TextMeshPro in 3D uses fontSize directly
                        var fontSizeProperty = tmp.GetType().GetProperty("fontSize");
                        float fontSize = fontSizeProperty != null ? (float)(fontSizeProperty.GetValue(tmp) ?? 1f) : 1f;
                        textWidth = prefWidth * (fontSize / 100f);
                        textHeight = prefHeight * (fontSize / 100f);
                    }
                }
            }
            
            // If preferred dimensions didn't work, try textBounds (most reliable for 3D)
            if (textWidth <= 0.1f || textHeight <= 0.1f)
            {
                var textBoundsProperty = tmp.GetType().GetProperty("textBounds");
                if (textBoundsProperty != null)
                {
                    Bounds bounds = (Bounds)textBoundsProperty.GetValue(tmp);
                    if (bounds.size.x > 0 && bounds.size.y > 0)
                    {
                        textWidth = bounds.size.x;
                        textHeight = bounds.size.y;
                    }
                }
            }
            
            // Final fallback: use renderer bounds (most reliable fallback)
            if (textWidth <= 0.1f || textHeight <= 0.1f)
            {
                Renderer renderer = labelObj.GetComponent<Renderer>();
                if (renderer != null && renderer.bounds.size.x > 0 && renderer.bounds.size.y > 0)
                {
                    Bounds bounds = renderer.bounds;
                    // Convert world bounds to local space
                    Vector3 localScale = labelObj.transform.lossyScale;
                    if (localScale.x > 0 && localScale.y > 0)
                    {
                        textWidth = bounds.size.x / localScale.x;
                        textHeight = bounds.size.y / localScale.y;
                    }
                    else
                    {
                        textWidth = bounds.size.x;
                        textHeight = bounds.size.y;
                    }
                }
            }
        }
        else
        {
            // Use TextMesh renderer bounds
            TextMesh textMesh = labelObj.GetComponent<TextMesh>();
            if (textMesh != null)
            {
                Renderer renderer = labelObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Bounds bounds = renderer.bounds;
                    // Convert to local space (with safety check to prevent division by zero)
                    Vector3 localScale = labelObj.transform.lossyScale;
                    if (Mathf.Abs(localScale.x) > 0.0001f && Mathf.Abs(localScale.y) > 0.0001f)
                    {
                        textWidth = bounds.size.x / localScale.x;
                        textHeight = bounds.size.y / localScale.y;
                    }
                    else
                    {
                        textWidth = bounds.size.x;
                        textHeight = bounds.size.y;
                    }
                    
                    // Validate results to prevent NaN/Infinity
                    if (float.IsNaN(textWidth) || float.IsInfinity(textWidth)) textWidth = 0.3f;
                    if (float.IsNaN(textHeight) || float.IsInfinity(textHeight)) textHeight = 0.15f;
                }
            }
        }
        
        // Validate and ensure minimum size (prevent NaN/Infinity)
        if (float.IsNaN(textWidth) || float.IsInfinity(textWidth) || textWidth <= 0)
            textWidth = 0.3f;
        if (float.IsNaN(textHeight) || float.IsInfinity(textHeight) || textHeight <= 0)
            textHeight = 0.15f;
        
        textWidth = Mathf.Max(0.3f, textWidth);
        textHeight = Mathf.Max(0.15f, textHeight);
        
        // Border is 3px larger (convert pixels to world units - assuming 100 pixels per unit)
        float borderPadding = 3f / 100f; // 3 pixels = 0.03 world units
        
        // Create rounded rectangle textures
        int bgWidth = 128;
        int bgHeight = 64;
        int borderWidth = bgWidth + 6; // 3px on each side
        int borderHeight = bgHeight + 6;
        int cornerRadius = 8;
        
        Texture2D bgTexture = CreateRoundedRectangleTexture(bgWidth, bgHeight, cornerRadius, new Color(0.5f, 0.5f, 0.5f, 0.7f)); // Grey, 70% opacity
        Texture2D borderTexture = CreateRoundedRectangleBorder(borderWidth, borderHeight, cornerRadius + 3, 3, Color.black); // Black border, 3px thick
        
        // Create border sprite (behind everything)
        GameObject borderObj = new GameObject("LabelBorder");
        borderObj.transform.SetParent(labelObj.transform);
        borderObj.transform.localPosition = Vector3.zero;
        borderObj.transform.localRotation = Quaternion.identity;
        
        SpriteRenderer borderRenderer = borderObj.AddComponent<SpriteRenderer>();
        Sprite borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(0.5f, 0.5f), 100f);
        borderRenderer.sprite = borderSprite;
        borderRenderer.sortingOrder = 98; // Behind background and text
        
        // Create background sprite (in front of border, behind text)
        GameObject bgObj = new GameObject("LabelBackground");
        bgObj.transform.SetParent(labelObj.transform);
        bgObj.transform.localPosition = Vector3.zero;
        bgObj.transform.localRotation = Quaternion.identity;
        
        SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        Sprite bgSprite = Sprite.Create(bgTexture, new Rect(0, 0, bgTexture.width, bgTexture.height), new Vector2(0.5f, 0.5f), 100f);
        bgRenderer.sprite = bgSprite;
        bgRenderer.sortingOrder = 99; // Behind text, in front of border
        
        // Scale to match text size exactly (with small padding for visual spacing)
        float padding = 0.05f; // Small padding around text for visual spacing
        bgObj.transform.localScale = new Vector3(textWidth + padding * 2, textHeight + padding * 2, 1f);
        borderObj.transform.localScale = new Vector3(textWidth + borderPadding * 2 + padding * 2, textHeight + borderPadding * 2 + padding * 2, 1f);
    }
    
    /// <summary>
    /// Creates a rounded rectangle texture with the specified dimensions, corner radius, and color
    /// </summary>
    Texture2D CreateRoundedRectangleTexture(int width, int height, int cornerRadius, Color color)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        
        // Fill with transparent
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        // Draw rounded rectangle
        int radiusSquared = cornerRadius * cornerRadius;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                
                // Check if pixel is inside the rounded rectangle
                bool inside = true;
                
                // Check corners
                if (x < cornerRadius && y < cornerRadius)
                {
                    // Top-left corner
                    int dx = x - cornerRadius;
                    int dy = y - cornerRadius;
                    if (dx * dx + dy * dy > radiusSquared)
                        inside = false;
                }
                else if (x >= width - cornerRadius && y < cornerRadius)
                {
                    // Top-right corner
                    int dx = x - (width - cornerRadius);
                    int dy = y - cornerRadius;
                    if (dx * dx + dy * dy > radiusSquared)
                        inside = false;
                }
                else if (x < cornerRadius && y >= height - cornerRadius)
                {
                    // Bottom-left corner
                    int dx = x - cornerRadius;
                    int dy = y - (height - cornerRadius);
                    if (dx * dx + dy * dy > radiusSquared)
                        inside = false;
                }
                else if (x >= width - cornerRadius && y >= height - cornerRadius)
                {
                    // Bottom-right corner
                    int dx = x - (width - cornerRadius);
                    int dy = y - (height - cornerRadius);
                    if (dx * dx + dy * dy > radiusSquared)
                        inside = false;
                }
                
                if (inside)
                {
                    pixels[index] = color;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    
    /// <summary>
    /// Creates a rounded rectangle border texture (hollow, only the border outline)
    /// </summary>
    Texture2D CreateRoundedRectangleBorder(int width, int height, int cornerRadius, int borderWidth, Color color)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        
        // Fill with transparent
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        // Draw rounded rectangle border
        int radiusSquared = cornerRadius * cornerRadius;
        int innerRadius = Mathf.Max(0, cornerRadius - borderWidth);
        int innerRadiusSquared = innerRadius * innerRadius;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                bool onBorder = false;
                
                // Check if pixel is within border area
                bool inOuterRect = (x >= 0 && x < width && y >= 0 && y < height);
                bool inInnerRect = (x >= borderWidth && x < width - borderWidth && 
                                    y >= borderWidth && y < height - borderWidth);
                
                if (inOuterRect && !inInnerRect)
                {
                    // Check corners
                    bool inCorner = false;
                    int cornerDistSquared = 0;
                    
                    if (x < cornerRadius && y < cornerRadius)
                    {
                        // Top-left corner
                        int dx = x - cornerRadius;
                        int dy = y - cornerRadius;
                        cornerDistSquared = dx * dx + dy * dy;
                        inCorner = true;
                    }
                    else if (x >= width - cornerRadius && y < cornerRadius)
                    {
                        // Top-right corner
                        int dx = x - (width - cornerRadius);
                        int dy = y - cornerRadius;
                        cornerDistSquared = dx * dx + dy * dy;
                        inCorner = true;
                    }
                    else if (x < cornerRadius && y >= height - cornerRadius)
                    {
                        // Bottom-left corner
                        int dx = x - cornerRadius;
                        int dy = y - (height - cornerRadius);
                        cornerDistSquared = dx * dx + dy * dy;
                        inCorner = true;
                    }
                    else if (x >= width - cornerRadius && y >= height - cornerRadius)
                    {
                        // Bottom-right corner
                        int dx = x - (width - cornerRadius);
                        int dy = y - (height - cornerRadius);
                        cornerDistSquared = dx * dx + dy * dy;
                        inCorner = true;
                    }
                    
                    if (inCorner)
                    {
                        // In corner area - check if within outer radius but outside inner radius
                        if (cornerDistSquared <= radiusSquared && cornerDistSquared > innerRadiusSquared)
                        {
                            onBorder = true;
                        }
                    }
                    else
                    {
                        // On straight edge
                        onBorder = true;
                    }
                }
                
                if (onBorder)
                {
                    pixels[index] = color;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    
    /// <summary>
    /// Gets the display name for a piece type
    /// </summary>
    string GetPieceDisplayName(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return "Pawn";
            case PieceType.Rook: return "Rook";
            case PieceType.Knight: return "Knight";
            case PieceType.Bishop: return "Bishop";
            case PieceType.Queen: return "Queen";
            case PieceType.King: return "King";
            default: return "Piece";
        }
    }
    
    /// <summary>
    /// Validates that a Vector3 doesn't contain NaN or Infinity values
    /// </summary>
    bool IsValidVector3(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
               !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
               !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
