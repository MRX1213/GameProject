# Chess VS ChatGPT

An innovative chess game that pits human players against OpenAI's ChatGPT AI as an opponent. Experience traditional chess with an experimental twist - after the opening phase, ChatGPT has a chance to "break the rules" and create chaotic, unpredictable gameplay situations.

## 🎮 Overview

**Chess VS ChatGPT** combines traditional chess mechanics with modern AI capabilities. Built in Unity 3D, the game features a full 3D chess board, piece models, and dual-camera perspectives. Players can choose to play as either White or Black, and the game includes complete chess rule implementation including check, checkmate, stalemate, castling, en passant, and pawn promotion.

### Key Features

- **Full Chess Implementation**: Complete chess rules including all special moves
- **AI Opponent**: Play against OpenAI's ChatGPT
- **Rule-Breaking Mode**: After move 6, ChatGPT has a 20% chance to break chess rules
- **3D Graphics**: Beautiful 3D chess board and pieces
- **Dual Camera System**: Different perspectives for White and Black players
- **Interactive UI**: Menu system, color selection, and game over screens

## 🎯 Game Mechanics

### Core Chess Mechanics

- Standard 8x8 chess board with all 32 pieces in starting positions
- Complete chess rule implementation:
  - Piece movement rules (pawn, rook, knight, bishop, queen, king)
  - Check and checkmate detection
  - Stalemate detection
  - Castling (kingside and queenside)
  - En passant capture
  - Pawn promotion (queen, rook, bishop, knight)
- Turn-based gameplay alternating between player and ChatGPT
- Move validation ensuring legal moves only for human player

### AI Opponent Mechanics (ChatGPT)

**Two-Phase Behavior System:**

1. **Phase 1 (Moves 1-6)**: Strict chess rule adherence (100% normal play)
2. **Phase 2 (Move 7+)**: Hybrid mode
   - 80% probability: Normal chess play following all rules
   - 20% probability: "Free Mode" - rule-breaking behavior

**Free Mode Capabilities:**
- Move pieces to any square (even if illegal by chess rules)
- Spawn new pieces of any type at any empty square
- Cannot capture own king (safety constraint)
- Cannot move enemy pieces (player's pieces)
- Must still get out of check if in check (critical constraint)

**Technical Features:**
- OpenAI API integration for move generation
- Move notation parsing (algebraic notation: e2e4, Nf3, O-O, etc.)
- Conversation history tracking for context-aware responses
- Fallback system: random legal moves if API fails or returns invalid moves

## 🎨 Visual Design

### 3D Environment
- Chess board: 8x8 grid of alternating light/dark squares
- Chess pieces: 3D models for all piece types
- Piece colors: White and Black (distinct materials/textures)
- Board markers: Sphere objects at each square for raycast detection

### Camera Perspectives
- **White camera**: Viewing from white's side (bottom of board)
- **Black camera**: Viewing from black's side (top of board)
- **Menu camera**: Overview or menu-specific angle

### Visual Effects
- Piece selection: Yellow outline/glow, 10% scale increase
- Valid move indicators: Highlighted destination squares
- Billboard text: Labels that rotate to face camera
- UI panels: Overlay panels for menus and game state

## 🕹️ Controls

### Primary Input Method: Mouse

- **Left Mouse Click (on piece)**: Select piece
  - Only works on player's own pieces
  - Only works on player's turn
  - Visual feedback: piece outline/glow appears, slight scale increase

- **Left Mouse Click (on empty square or enemy piece)**: Move selected piece
  - Only works if square is a valid move destination
  - Captures enemy pieces automatically
  - Triggers turn change to ChatGPT
  - Visual feedback: piece moves to new position

### UI Button Clicks
- **Start Menu**: "Play" button, "Exit" button
- **Color Selection**: "Play as White", "Play as Black"
- **Game Over**: "Restart" button, "Main Menu" button
- **Promotion**: Queen/Rook/Bishop/Knight buttons

**Note**: Keyboard support is not implemented (mouse-only interface)

## 📋 Requirements

### Software
- Unity 3D (version compatible with the project)
- OpenAI API Key (get it from https://platform.openai.com/account/api-keys)

### Setup
1. Open the project in Unity
2. In the `ChatGPTChessPlayer` component, enter your OpenAI API key
3. Build and run the project

## 🎲 How to Play

1. **Start the Game**: Launch the application
2. **Choose Your Color**: Select to play as White or Black
3. **Play Chess**: Make moves by clicking on pieces and then clicking on destination squares
4. **Adapt to Chaos**: After move 6, be prepared for ChatGPT's potential rule-breaking moves
5. **Win the Game**: Achieve checkmate or force a stalemate

### Game Flow

1. Application starts with the start menu
2. Click "Play" to proceed to color selection
3. Choose White or Black
4. Game loads with board in starting position
5. If playing as Black, ChatGPT (White) makes the first move
6. Alternate turns between player and ChatGPT
7. Game ends on checkmate or stalemate
8. Restart or return to main menu

## 🏗️ Technical Architecture

### Core Systems

1. **ChessBoardWithPieces.cs**
   - Board state management (8x8 array)
   - Move validation and execution
   - Check/checkmate/stalemate detection
   - Piece spawning system (for ChatGPT free mode)
   - Raycast-based input handling
   - Square name conversion (a1-h8 notation)

2. **ChatGPTChessPlayer.cs**
   - OpenAI API integration
   - Move generation and parsing
   - Free mode logic (20% chance after move 6)
   - Conversation history management
   - Fallback move generation

3. **ChessUIManager.cs**
   - Scene management
   - UI panel control
   - Camera switching
   - Game flow orchestration
   - Promotion UI handling

4. **ChessPieceComponent.cs**
   - Component attached to piece GameObjects
   - Stores piece type and color metadata

5. **PieceOutline.cs**
   - Visual highlighting system
   - Material emission control
   - Scale animation

6. **BillboardText.cs**
   - Camera-facing text labels
   - Dynamic camera reference updates

### API Integration

- **Endpoint**: `https://api.openai.com/v1/chat/completions`
- **Model**: `gpt-4.1-mini` (configurable)
- **Request format**: JSON with conversation history
- **Response parsing**: Extract move notation from text response
- **Error handling**: Fallback to random legal moves

## 📐 Special Rules and Constraints

### ChatGPT Constraints (Always Enforced)
1. Cannot capture its own king
2. Cannot move enemy pieces (player's pieces)
3. Must get out of check if in check (cannot ignore check)
4. Cannot move king into check (king safety)

### ChatGPT Free Mode (20% chance after move 6)
- Can move pieces to any square (even if illegal)
- Can spawn new pieces at empty squares
- Still subject to constraints above
- Can break piece movement rules
- Can create multiple pieces of same type

### Player Constraints
- Must follow all chess rules always
- Cannot break rules
- Standard move validation applies

### Move Notation
- Standard algebraic: `e2e4`, `Nf3`, `O-O`, `O-O-O`
- Promotion: `e7e8Q`, `e7e8=Q`
- ChatGPT can use various formats, system parses flexibly

## 🎭 MDA Framework Analysis

### Mechanics (What the game does)
- Core chess rules and piece movements
- AI opponent with two-phase behavior system
- Mouse-based player interaction
- Visual feedback systems
- Scene and game flow management

### Dynamics (What emerges from the mechanics)
- **Strategic Adaptation**: Players balance traditional chess strategy with anticipation of rule-breaking moves
- **Unpredictability**: 20% free mode chance creates surprise moments
- **Learning**: Players learn to recognize and respond to ChatGPT's patterns
- **Competitive Tension**: Uncertainty about opponent's capabilities adds excitement
- **Narrative Emergence**: Each game tells a unique story through moves

### Aesthetics (What the player feels)
- **Challenge**: Intellectual challenge of chess plus adapting to unpredictable AI
- **Discovery**: Surprise at creative moves and exploration of rule boundaries
- **Expression**: Players express their chess style and adaptation strategies
- **Fantasy**: Playing against an AI that thinks creatively
- **Sensation**: Visual satisfaction of 3D chess pieces and board
- **Fellowship**: Sharing experiences of unusual moves
- **Narrative**: Each game as a story of conflict and adaptation

## 🎯 Design Philosophy

### Core Concept
Chess VS ChatGPT explores the intersection of traditional strategy games and modern AI capabilities. By allowing ChatGPT to occasionally break chess rules, the game creates a unique experience that cannot be replicated in standard chess.

### Design Goals
1. Maintain chess as the core experience (first 6 moves are pure chess)
2. Introduce controlled chaos (20% free mode, not 100%)
3. Ensure player always has agency (player never breaks rules)
4. Create moments of surprise and delight
5. Balance challenge and accessibility

### Target Audience
- Chess enthusiasts looking for a novel experience
- Players interested in AI-human interaction
- Gamers who enjoy experimental game mechanics
- Casual players who want to try something different

## 🚀 Future Expansion Possibilities

- Difficulty settings (adjust free mode probability)
- Custom rule sets
- Move history and replay system
- Statistics tracking
- Multiplayer mode (two humans, ChatGPT as referee/commentator)
- Tournament mode
- Custom board themes and piece sets

## 📝 License

[Add your license information here]

## 👥 Credits

[Add credits and acknowledgments here]

---

**Version**: 1.0  
**Platform**: Unity 3D (PC/Desktop)  
**Genre**: Strategy, Puzzle, Experimental AI Game

