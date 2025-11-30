using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChessUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject startMenuPanel;      // First panel: Text, Play, Exit
    public GameObject colorSelectionPanel; // Second panel: Choose Color, White, Black
    public GameObject gameOverPanel;
    
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
    
    [Header("Game References")]
    public ChessBoardWithPieces chessGame;
    public ChatGPTChessPlayer chatGPTPlayer;
    
    [Header("Cameras")]
    public Camera menuCamera;   // Camera for menu (active before game starts)
    public Camera whiteCamera;  // Camera for white's perspective
    public Camera blackCamera;  // Camera for black's perspective
    
    private bool gameStarted = false;
    private PieceColor playerColor = PieceColor.White;
    
    void Start()
    {
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
        
        // Initialize UI
        if (startMenuPanel != null)
            startMenuPanel.SetActive(true);
        if (colorSelectionPanel != null)
            colorSelectionPanel.SetActive(false);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        // Setup button listeners
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
        
        // Set default text
        if (colorSelectionText != null)
            colorSelectionText.text = "Choose Your Color";
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
        // Check for game over
        if (gameStarted && chessGame != null && chessGame.IsGameOver())
        {
            ShowGameOver();
        }
    }
    
    void StartGame(PieceColor selectedColor)
    {
        playerColor = selectedColor;
        gameStarted = true;
        
        // Switch to the appropriate camera
        SwitchCamera(playerColor);
        
        // Hide all UI panels
        if (startMenuPanel != null)
            startMenuPanel.SetActive(false);
        if (colorSelectionPanel != null)
            colorSelectionPanel.SetActive(false);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        // Initialize game
        if (chessGame != null)
        {
            chessGame.SetPlayerColor(playerColor);
            chessGame.SetChatGPTPlayer(chatGPTPlayer);
            
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
        }
        else
        {
            if (blackCamera != null)
            {
                blackCamera.gameObject.SetActive(true);
                // Set as main camera
                blackCamera.tag = "MainCamera";
                if (whiteCamera != null)
                    whiteCamera.tag = "Untagged";
            }
        }
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
        // Reset game state
        gameStarted = false;
        
        // Disable game cameras, enable menu camera
        if (whiteCamera != null)
        {
            whiteCamera.gameObject.SetActive(false);
            whiteCamera.tag = "Untagged";
        }
        if (blackCamera != null)
        {
            blackCamera.gameObject.SetActive(false);
            blackCamera.tag = "Untagged";
        }
        if (menuCamera != null)
        {
            menuCamera.gameObject.SetActive(true);
            menuCamera.tag = "MainCamera";
        }
        
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (colorSelectionPanel != null)
            colorSelectionPanel.SetActive(false);
        if (startMenuPanel != null)
            startMenuPanel.SetActive(true);
        
        // Reset chess game
        if (chessGame != null)
        {
            chessGame.ResetGame();
        }
        
        if (chatGPTPlayer != null)
        {
            chatGPTPlayer.Reset();
        }
    }
    
    void ReturnToMainMenu()
    {
        RestartGame();
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
