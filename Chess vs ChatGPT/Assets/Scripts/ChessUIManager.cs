using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class ChessUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject startMenuPanel;      // First panel: Text, Play, Exit
    public GameObject colorSelectionPanel; // Second panel: Choose Color, White, Black
    public GameObject gameOverPanel;
    public GameObject promotionPanel;      // Panel for pawn promotion choice
    
    [Header("Start Menu UI")]
    public TextMeshProUGUI startMenuText;
    public Button playButton;
    public Button exitButton;
    
    [Header("Color Selection UI")]
    public TextMeshProUGUI colorSelectionText;
    public Button playAsWhiteButton;
    public Button playAsBlackButton;
    
    [Header("Game Over UI")]
    public TextMeshProUGUI gameOverText;
    public Button restartButton;
    public Button mainMenuButton;
    
    [Header("Promotion UI")]
    public TextMeshProUGUI promotionText;
    public Button queenButton;
    public Button rookButton;
    public Button bishopButton;
    public Button knightButton;
    
    [Header("Game References")]
    public ChessBoardWithPieces chessGame;
    public ChatGPTChessPlayer chatGPTPlayer;
    public GameObject chessBoardPrefab; // Reference to the ChessBoardWithPieces prefab
    
    [Header("Cameras")]
    public Camera menuCamera;   // Camera for menu (active before game starts)
    public Camera whiteCamera;  // Camera for white's perspective
    public Camera blackCamera;  // Camera for black's perspective
    
    private bool gameStarted = false;
    private PieceColor playerColor = PieceColor.White;
    private System.Action<PieceType> promotionCallback = null; // Callback for promotion choice
    private static int gamesPlayed = 0; // Counter for number of games played (static to persist across scene reloads)
    private static bool shouldAutoStart = false; // Flag to auto-start game after scene reload
    private static PieceColor autoStartColor = PieceColor.White; // Color to use when auto-starting
    
    [Header("Scene Settings")]
    public string chessGameSceneName = "ChessGame"; // Name of the chess game scene
    public string startingScreenSceneName = "StartingScreen"; // Name of the starting screen scene
    
    void Start()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        
        // Find cameras by name if not assigned
        if (menuCamera == null)
        {
            GameObject menuCamObj = GameObject.Find("MenuCamera");
            if (menuCamObj != null)
                menuCamera = menuCamObj.GetComponent<Camera>();
        }
        
        if (whiteCamera == null)
        {
            GameObject whiteCamObj = GameObject.Find("_MainCameraWhite");
            if (whiteCamObj != null)
                whiteCamera = whiteCamObj.GetComponent<Camera>();
        }
        
        if (blackCamera == null)
        {
            GameObject blackCamObj = GameObject.Find("_MainCameraBlack");
            if (blackCamObj != null)
                blackCamera = blackCamObj.GetComponent<Camera>();
        }
        
        // Setup button listeners (works in both scenes)
        if (playButton != null)
            playButton.onClick.AddListener(ShowColorSelection);
        if (exitButton != null)
            exitButton.onClick.AddListener(ExitGame);
        if (playAsWhiteButton != null)
            playAsWhiteButton.onClick.AddListener(() => StartGame(PieceColor.White));
        if (playAsBlackButton != null)
            playAsBlackButton.onClick.AddListener(() => StartGame(PieceColor.Black));
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        
        // Setup promotion button listeners
        if (queenButton != null)
            queenButton.onClick.AddListener(() => SelectPromotion(PieceType.Queen));
        if (rookButton != null)
            rookButton.onClick.AddListener(() => SelectPromotion(PieceType.Rook));
        if (bishopButton != null)
            bishopButton.onClick.AddListener(() => SelectPromotion(PieceType.Bishop));
        if (knightButton != null)
            knightButton.onClick.AddListener(() => SelectPromotion(PieceType.Knight));
        
        // Set default text
        if (colorSelectionText != null)
            colorSelectionText.text = "Choose Your Color";
        
        // Check which scene we're in
        if (currentSceneName == startingScreenSceneName)
        {
            // We're in the StartingScreen scene - show menu UI
            // Enable menu camera, disable game cameras initially
            if (menuCamera != null)
            {
                menuCamera.gameObject.SetActive(true);
                menuCamera.tag = "MainCamera";
            }
            if (whiteCamera != null)
                whiteCamera.gameObject.SetActive(false);
            if (blackCamera != null)
                blackCamera.gameObject.SetActive(false);
            
            // Initialize UI for starting screen
            if (startMenuPanel != null)
                startMenuPanel.SetActive(true);
            if (colorSelectionPanel != null)
                colorSelectionPanel.SetActive(false);
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            if (promotionPanel != null)
                promotionPanel.SetActive(false);
        }
        else if (currentSceneName == chessGameSceneName)
        {
            // We're in the ChessGame scene
            // Reassign references on start in case they're lost
            ReassignReferences();
            
            // Check if we should auto-start the game
            if (shouldAutoStart)
            {
                Debug.Log($"In ChessGame scene, auto-starting with color: {autoStartColor}");
                InitializeGameInChessScene();
            }
            else
            {
                // If not auto-starting, hide menu panels (they shouldn't be in this scene anyway)
                if (startMenuPanel != null)
                    startMenuPanel.SetActive(false);
                if (colorSelectionPanel != null)
                    colorSelectionPanel.SetActive(false);
                if (gameOverPanel != null)
                    gameOverPanel.SetActive(false);
                if (promotionPanel != null)
                    promotionPanel.SetActive(false);
            }
        }
    }
    
    void ShowColorSelection()
    {
        // Hide start menu, show color selection
        if (startMenuPanel != null)
            startMenuPanel.SetActive(false);
        if (colorSelectionPanel != null)
            colorSelectionPanel.SetActive(true);
    }
    
    void ExitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    void Update()
    {
        // Reassign references if they're lost (can happen after respawn)
        if (chessGame == null || chatGPTPlayer == null)
        {
            ReassignReferences();
        }
        
        // Check for game over
        if (gameStarted && chessGame != null && chessGame.IsGameOver())
        {
            ShowGameOver();
        }
    }
    
    void StartGame(PieceColor selectedColor)
    {
        playerColor = selectedColor;
        
        // Store the color choice for the game scene
        autoStartColor = selectedColor;
        shouldAutoStart = true;
        
        Debug.Log($"Loading ChessGame scene with player color: {selectedColor}");
        
        // Load the ChessGame scene
        SceneManager.LoadScene(chessGameSceneName);
    }
    
    // This method is called when the ChessGame scene loads
    public void InitializeGameInChessScene()
    {
        if (!shouldAutoStart)
        {
            Debug.LogWarning("InitializeGameInChessScene called but shouldAutoStart is false");
            return;
        }
        
        shouldAutoStart = false; // Reset flag
        playerColor = autoStartColor;
        gameStarted = true;
        
        Debug.Log($"InitializeGameInChessScene: Restored playerColor={playerColor} from autoStartColor={autoStartColor}");
        
        // Start coroutine to initialize after everything loads
        StartCoroutine(InitializeGameCoroutine());
    }
    
    IEnumerator InitializeGameCoroutine()
    {
        // Wait for scene to fully load
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        // Reassign references after scene load
        ReassignReferences();
        
        // Switch to the appropriate camera
        SwitchCamera(playerColor);
        
        // Hide all UI panels (if they exist in this scene)
        if (startMenuPanel != null)
            startMenuPanel.SetActive(false);
        if (colorSelectionPanel != null)
            colorSelectionPanel.SetActive(false);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (promotionPanel != null)
            promotionPanel.SetActive(false);
        
        // Reset the chess board (will check gamesPlayed counter)
        RespawnChessBoard();
        
        // Wait a frame for reset to complete
        yield return new WaitForEndOfFrame();
        
        // Reassign references again after reset
        ReassignReferences();
        
        // Initialize game with the board
        if (chessGame != null)
        {
            chessGame.SetPlayerColor(playerColor);
            chessGame.SetChatGPTPlayer(chatGPTPlayer);
            chessGame.SetUIManager(this); // Connect UI manager for promotion
            
            // If player is black, set initial turn to white (ChatGPT's turn)
            if (playerColor == PieceColor.Black)
            {
                chessGame.SetTurn(true); // Set to white's turn (ChatGPT)
            }
            
            // Update camera reference after switching cameras
            chessGame.UpdateCameraReference();
        }
        
        if (chatGPTPlayer != null)
        {
            chatGPTPlayer.Initialize(playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White);
            chatGPTPlayer.SetChessGame(chessGame);
            
            // If player is black, ChatGPT (white) should make the first move
            if (playerColor == PieceColor.Black)
            {
                Debug.Log("Player is black, ChatGPT (white) will make the first move");
                chatGPTPlayer.MakeFirstMove();
            }
        }
        
        Debug.Log($"Game started! Player is {playerColor}, ChatGPT is {(playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White)}");
    }
    
    void SwitchCamera(PieceColor color)
    {
        // Disable menu camera
        if (menuCamera != null)
        {
            menuCamera.gameObject.SetActive(false);
            menuCamera.tag = "Untagged";
        }
        
        // Disable both game cameras first
        if (whiteCamera != null)
            whiteCamera.gameObject.SetActive(false);
        if (blackCamera != null)
            blackCamera.gameObject.SetActive(false);
        
        // Enable the appropriate camera
        // White player sees from white side, Black player sees from black side
        if (color == PieceColor.White)
        {
            if (whiteCamera != null)
            {
                whiteCamera.gameObject.SetActive(true);
                // Set as main camera
                whiteCamera.tag = "MainCamera";
                if (blackCamera != null)
                    blackCamera.tag = "Untagged";
            }
            else
            {
                Debug.LogWarning("White camera not found! Player is White but whiteCamera is null.");
            }
        }
        else // color == PieceColor.Black
        {
            if (blackCamera != null)
            {
                blackCamera.gameObject.SetActive(true);
                // Set as main camera
                blackCamera.tag = "MainCamera";
                if (whiteCamera != null)
                    whiteCamera.tag = "Untagged";
            }
            else
            {
                Debug.LogWarning("Black camera not found! Player is Black but blackCamera is null.");
            }
        }
        
        Debug.Log($"Switched camera for {color} player. White camera active: {whiteCamera != null && whiteCamera.gameObject.activeSelf}, Black camera active: {blackCamera != null && blackCamera.gameObject.activeSelf}");
    }
    
    void ShowGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverText != null && chessGame != null)
            {
                gameOverText.text = chessGame.GetGameOverMessage();
            }
        }
    }
    
    void RestartGame()
    {
        // Reset game counter and flags
        gamesPlayed = 0;
        shouldAutoStart = false;
        
        Debug.Log("RestartGame: Returning to StartingScreen scene for color selection");
        
        // Load the StartingScreen scene to allow color selection again
        SceneManager.LoadScene(startingScreenSceneName);
    }
    
    IEnumerator ReassignReferencesAfterRespawn()
    {
        // Wait a frame for respawn to complete
        yield return new WaitForEndOfFrame();
        ReassignReferences();
    }
    
    public void ShowPromotionPanel(PieceColor pawnColor, System.Action<PieceType> callback)
    {
        promotionCallback = callback;
        
        // Hide promotion text (leave it empty)
        if (promotionText != null)
        {
            promotionText.text = "";
        }
        
        // Show the panel
        if (promotionPanel != null)
        {
            promotionPanel.SetActive(true);
        }
        
        Debug.Log($"Showing promotion panel for {pawnColor} pawn");
    }
    
    void SelectPromotion(PieceType promotionType)
    {
        Debug.Log($"Player selected promotion to {promotionType}");
        
        // Hide the panel
        if (promotionPanel != null)
        {
            promotionPanel.SetActive(false);
        }
        
        // Call the callback if it exists
        if (promotionCallback != null)
        {
            promotionCallback(promotionType);
            promotionCallback = null;
        }
    }
    
    void RespawnChessBoard()
    {
        // If this is the first game (gamesPlayed == 0), don't reload scene, just reset
        if (gamesPlayed == 0)
        {
            Debug.Log("First game - using ResetGame() without reloading scene.");
            if (chessGame != null && chessGame.gameObject != null)
            {
                chessGame.ResetGame();
            }
            else
            {
                // Try to find the board in the scene
                chessGame = FindObjectOfType<ChessBoardWithPieces>();
                if (chessGame != null)
                {
                    chessGame.ResetGame();
                }
            }
            return;
        }
        
        // For games after the first one, reload the scene to reset everything
        Debug.Log($"Games played: {gamesPlayed} - Reloading scene to reset board and pieces");
        ReloadScene();
    }
    
    void ReloadScene()
    {
        // Reload the current scene to reset everything
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    // Method to find and reassign references if they're lost
    void ReassignReferences()
    {
        // Find chess game if reference is lost
        if (chessGame == null)
        {
            chessGame = FindObjectOfType<ChessBoardWithPieces>();
            if (chessGame != null)
            {
                Debug.Log("Reassigned chessGame reference from scene");
            }
            else
            {
                Debug.LogWarning("Could not find ChessBoardWithPieces in scene to reassign reference");
            }
        }
        
        // Find ChatGPT player if reference is lost
        if (chatGPTPlayer == null)
        {
            chatGPTPlayer = FindObjectOfType<ChatGPTChessPlayer>();
            if (chatGPTPlayer != null)
            {
                Debug.Log("Reassigned chatGPTPlayer reference from scene");
            }
        }
        
        // Try to preserve prefab reference - if it's null, try to find it
        if (chessBoardPrefab == null)
        {
            // Try to load from Resources
            chessBoardPrefab = Resources.Load<GameObject>("ChessBoardWithPieces");
            if (chessBoardPrefab != null)
            {
                Debug.Log("Reassigned chessBoardPrefab from Resources");
            }
        }
    }
    
    void ReturnToMainMenu()
    {
        // Reset game counter and flags
        gamesPlayed = 0;
        shouldAutoStart = false;
        
        // Load the StartingScreen scene
        Debug.Log("Returning to StartingScreen scene");
        SceneManager.LoadScene(startingScreenSceneName);
    }
    
    public bool IsGameStarted()
    {
        return gameStarted;
    }
    
    public PieceColor GetPlayerColor()
    {
        return playerColor;
    }
}
