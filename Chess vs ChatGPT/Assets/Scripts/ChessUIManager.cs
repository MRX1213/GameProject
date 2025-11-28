using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChessUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject gameOverPanel;
    
    [Header("Main Menu UI")]
    public Button playAsWhiteButton;
    public Button playAsBlackButton;
    public TextMeshProUGUI titleText;
    
    [Header("Game Over UI")]
    public TextMeshProUGUI gameOverText;
    public Button restartButton;
    public Button mainMenuButton;
    
    [Header("Game References")]
    public ChessBoardWithPieces chessGame;
    public ChatGPTChessPlayer chatGPTPlayer;
    
    private bool gameStarted = false;
    private PieceColor playerColor = PieceColor.White;
    
    void Start()
    {
        // Initialize UI
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        
        // Setup button listeners
        if (playAsWhiteButton != null)
            playAsWhiteButton.onClick.AddListener(() => StartGame(PieceColor.White));
        if (playAsBlackButton != null)
            playAsBlackButton.onClick.AddListener(() => StartGame(PieceColor.Black));
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
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
        
        // Hide main menu
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        
        // Initialize game
        if (chessGame != null)
        {
            chessGame.SetPlayerColor(playerColor);
            chessGame.SetChatGPTPlayer(chatGPTPlayer);
        }
        
        if (chatGPTPlayer != null)
        {
            chatGPTPlayer.Initialize(playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White);
            chatGPTPlayer.SetChessGame(chessGame);
        }
        
        Debug.Log($"Game started! Player is {playerColor}, ChatGPT is {(playerColor == PieceColor.White ? PieceColor.Black : PieceColor.White)}");
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
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
        
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
