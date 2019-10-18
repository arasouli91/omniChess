using System.Collections;
using System.Collections.Generic;
using UnityEngine;


using UnityEngine.Advertisements;

public class Board : MonoBehaviour
{
    private readonly int ROWS = Settings.ROWS | 8, COLS = Settings.COLS | 8;
    Square[][] Grid;
    private int selectionX = -1, selectionY = -1, selectionX1 = -1, selectionY1 = -1, revivalPosX = -1, revivalPosY = -1;
    private GameObject selection = null, selectedPiece = null;
    private int teamTurn = 1; // -1 for white, 1 for black
    // Used to determine whether or not a move can be made
    private Dictionary<int, HashSet<int>> validLocations;
    // Count of dead units for each player
    private int[] deadCount;
    private GameObject blackSquare, whiteSquare;
    // Base pieces initially off screen, duplicated for board setup
    GameObject[] basePieces;
    // The actual pieces on the board
    private List<GameObject>[] thePieces;
    private GameObject cam;
    private Vector3 camDefaultPos, camDefaultRot;
    private bool checkedFlag = false, reviveFlag = false, wokeFlag = false, revivedFlag = false, highLit = false;
    [SerializeField]
    // Square materials
    private Material a1,a2,a3,a4;
    [SerializeField]
    private GameObject blackWins, whiteWins, gameOver;
    private King[] kings;   // Always need to know where each player's king is
    private bool[] kingPlaced;
    private readonly int BLACK = 0, WHITE = 1, BLACK_KING = 5, WHITE_KING = 10;

    void PlacePiece(GameObject piece, int row, int col)
        => Grid[row][col].PieceHeld = Object.Instantiate(piece, new Vector3(col, 1, row), Quaternion.identity);

    void Start ()
    {
        #region Init
#if UNITY_ANDROID
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        QualitySettings.antiAliasing = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#endif
        Settings.HoverFlag = true;

        Grid = new Square[ROWS][];
        basePieces = new GameObject[12];
        thePieces = new List<GameObject>[2];
        thePieces[BLACK] = new List<GameObject>(); thePieces[WHITE] = new List<GameObject>();
        kings = new King[2];
        kingPlaced = new bool[2];
        deadCount = new int[2];
        EventCallbacks.ReviveEvent.RegisterListener(HandleRevival);

        // Find initial squares and units
        blackSquare = GameObject.Find("bBlackSquare1");
        whiteSquare = GameObject.Find("bWhiteSquare1");
        {
            basePieces[0] = GameObject.Find("Black Pawn");
            // All these will be deleted, because their positions are variable they were initial off screen
            basePieces[6] = GameObject.Find("White Pawn");
            basePieces[7] = GameObject.Find("White Rook");
            basePieces[8] = GameObject.Find("White Knight");
            basePieces[9] = GameObject.Find("White Bishop");
            basePieces[10] = GameObject.Find("White King");
            basePieces[11] = GameObject.Find("White Queen");
            basePieces[1] = GameObject.Find("Black Rook");
            basePieces[2] = GameObject.Find("Black Knight");
            basePieces[3] = GameObject.Find("Black Bishop");
            basePieces[4] = GameObject.Find("Black Queen");
            basePieces[5] = GameObject.Find("Black King");
        }

        // Auto generate back row sequence, MUST have even cols
        // The values placed in BackRow[] correspond to the black pieces held in pieces[]
        //  the white pieces are determined by shifting 6 indices
        var BackRow = new int[COLS];
        for(int i = 0; i < (COLS-2)/2; ++i)
        {   // Pieces placed symmetrically
            BackRow[i] = BackRow[COLS - i - 1] = (i)%3 + 1; // loop through possible pieces
        }
        BackRow[(COLS / 2) - 1] = 4;
        BackRow[(COLS / 2)] = 5;

        #endregion Init

        #region Board Setup

        // Place all squares and pieces on board
        for (int row = 0; row < ROWS; row++)
        {
            Grid[row] = new Square[COLS];
            for (int col = 0; col < COLS; col++)
            {
                PlaceSquare(row, col);

                // Place pieces
                if (Settings.IsRandomMode)
                    RandomPlacement(row, col);
                else
                    StandardPlacement(BackRow, row, col);

                // Handle setting team and adding to list
                FinalizePiece(row, col);
            }
        }

        #endregion Board Setup

        #region Setup Other Pieces

        //// Position/resize all other objects in scene ////

        // Camera and lights need to be positioned accordingly.
        // Directional light and camera need to be positioned in middle of platform
        // Find camera and lights
        cam = GameObject.Find("Main Camera");
        camDefaultPos = cam.transform.localPosition;
        camDefaultRot = cam.transform.eulerAngles;
        var l = GameObject.Find("Directional Light");
        var l1 = GameObject.Find("Point Light");
        var l2 = GameObject.Find("Point Light Top");

        // Give them initial positions. 
        // Orderded by 3 on bottom left 2 top left.  3 right bottom 2 right top. 1 white bottom/top. 1 black bottom/top
        GameObject[] lights = new GameObject[14]; lights[0] = l1; lights[3] = l2;
        {
            lights[1] = Object.Instantiate(l1, new Vector3(-5, 1, 4), Quaternion.identity);
            lights[2] = Object.Instantiate(l1, new Vector3(-5, 1, 13), Quaternion.identity);
            lights[4] = Object.Instantiate(l1, new Vector3(-1, 5, 10), Quaternion.identity);

            lights[5] = Object.Instantiate(l1, new Vector3(15, 1, 4), Quaternion.identity);
            lights[6] = Object.Instantiate(l1, new Vector3(15, 1, 13), Quaternion.identity);
            lights[7] = Object.Instantiate(l1, new Vector3(15, 1, -5), Quaternion.identity);
            lights[8] = Object.Instantiate(l1, new Vector3(10, 5, 0), Quaternion.identity);
            lights[9] = Object.Instantiate(l1, new Vector3(10, 5, 10), Quaternion.identity);

            lights[10] = Object.Instantiate(l1, new Vector3(5, 1, 15), Quaternion.identity);
            lights[11] = Object.Instantiate(l1, new Vector3(5, 5, 10), Quaternion.identity);
            lights[12] = Object.Instantiate(l1, new Vector3(5, 1, -5), Quaternion.identity);
            lights[13] = Object.Instantiate(l1, new Vector3(5, 5, 0), Quaternion.identity);
        }

        // Adjust all side and corner pieces
        // Position camera and lights
        int cDiff = 0 , rDiff = 0;
        if (COLS > 8)
        {
            cDiff = COLS - 8;
        }
        if (ROWS > 8)
        {
            rDiff = ROWS - 8;
            GameObject bin = GameObject.Find("CartWhite");
            bin.transform.localPosition = new Vector3(-6.66f, -1, ROWS);
        }
        if (COLS>8 || ROWS > 8) {
            // Collision plane
            GameObject plane = GameObject.Find("Plane");
            plane.transform.localPosition = new Vector3(COLS / 2f - .5f, 0.5f, ROWS / 2f - .5f);
            plane.transform.localScale = new Vector3(COLS / 10f, 0.01f, ROWS / 10f);

            // Lights Orderded by 3 on bottom left 2 top left.  3 right bottom 2 right top. 1 white bottom/top. 1 black bottom/top
            // need to move: [1,2]U[4] pos Z  --- [6,7]U[9] pos Z & pos X
            // 5,8  pos X  ---   10,11 pos X,Z   ---- 12,13 pos X

            // Adjust all affected side pieces, and platform
            // bSideVertical needs to scale by diff and translate by 0.5*(diff) into pos. Z
            GameObject cur = GameObject.Find("bSideVertical");
            cur.transform.localPosition += new Vector3(0, 0, 0.5f*(rDiff));
            cur.transform.localScale += new Vector3(rDiff,0,0);
            for(int i = 1; i <5; ++i)   
            {
                if (i != 3) lights[i].transform.localPosition += new Vector3(0, 0, 0.5f * (rDiff));
            }

            // bCornerLeftFar needs to translate
            cur = GameObject.Find("bCornerLeftFar");
            cur.transform.localPosition += new Vector3(0, 0, rDiff);

            // bSideVerticalRight needs to scale and translate
            cur = GameObject.Find("bSideVerticalRight");
            cur.transform.localPosition += new Vector3(cDiff, 0, 0.5f * (rDiff));
            cur.transform.localScale += new Vector3(rDiff, 0, 0);
            for (int i = 6; i < 10; ++i)
            {
                if (i != 8) lights[i].transform.localPosition += new Vector3(cDiff, 0, 0.5f * (rDiff));
            }


            // bCornerRightFar needs to translate
            cur = GameObject.Find("bCornerRightFar");
            cur.transform.localPosition += new Vector3(cDiff, 0, rDiff);

            // bSideHorizontalFar needs to translate and scale
            cur = GameObject.Find("bSideHorizontalFar");
            cur.transform.localPosition += new Vector3(0.5f * (cDiff), 0, rDiff);
            cur.transform.localScale += new Vector3(cDiff, 0, 0);
            lights[10].transform.localPosition += new Vector3(0.5f * (cDiff), 0, rDiff);
            lights[11].transform.localPosition += new Vector3(0.5f * (cDiff), 0, rDiff);

            cur = GameObject.Find("bPlatform");
            cur.transform.localPosition += new Vector3(0.5f * (cDiff), 0, 0.5f * (rDiff));
            cur.transform.localScale += new Vector3(rDiff, 0, cDiff);
        
            cur = GameObject.Find("bSideHorizontal");
            cur.transform.localPosition += new Vector3(0.5f * (cDiff),0,0);
            cur.transform.localScale += new Vector3(cDiff,0,0);
            lights[12].transform.localPosition += new Vector3(0.5f * (cDiff), 0, 0);
            lights[13].transform.localPosition += new Vector3(0.5f * (cDiff), 0, 0);

            cur = GameObject.Find("bCornerRight");
            cur.transform.localPosition += new Vector3(cDiff,0,0);
            lights[5].transform.localPosition += new Vector3(cDiff, 0, rDiff);
            lights[8].transform.localPosition += new Vector3(cDiff, 0, rDiff);
            
            cam.transform.localPosition = new Vector3(cDiff/2+ 3.5f, 9.8f, -6);
            camDefaultPos = cam.transform.localPosition;
            l.transform.localPosition += new Vector3(cDiff / 2+ 3.5f, 0, 0);
            cam.GetComponent<CameraController>().panLimitLeftTop += new Vector2(0,rDiff);
            cam.GetComponent<CameraController>().panLimitRightBot += new Vector2(cDiff, 0);

        }

        #endregion Setup Other Pieces

        // Delete offboard base pieces, delete black/white offboard squares
        GameObject.Destroy(blackSquare);
        GameObject.Destroy(whiteSquare);
        for (int i = 0; i < 12; ++i)
            GameObject.Destroy(basePieces[i]);
    }

    // Place a square object in the board during setup
    void PlaceSquare(int row, int col)
    {
        Grid[row][col] = new Square();
        GameObject square;
        if ((row + col) % 2 == 0)   // alternate square colors
            square = blackSquare;
        else
            square = whiteSquare;

        Grid[row][col].Cube = Object.Instantiate(square, new Vector3(col, 0, row), Quaternion.identity);
    }

    void RandomPlacement(int row, int col)
    {
        int pieceNumber;

        // Place randomly chosen pieces
        if (row == 1)   // Black front row
        {   // Randomly generate a piece number avoiding kings
            PlacePiece(basePieces[Random.Range(0, 5)], row, col);
        }
        else if (row == ROWS - 2)   // White front row
        {   // Randomly generate a piece number avoiding kings
            do pieceNumber = Random.Range(6, 12); //[6,12)
            while (pieceNumber == 10);

            PlacePiece(basePieces[pieceNumber], row, col);
        }
        else if (row == ROWS - 1) // White back row
        {
            RandomBackRowPlacement(WHITE, WHITE_KING, row, col, 6);
        }
        else if (row == 0) // Black back row
        {
            RandomBackRowPlacement(BLACK, BLACK_KING, row, col);
        }
    }

    void StandardPlacement(int[] BackRow, int row, int col)
    {
        // Place pieces
        if (row == 1)   // Black front row
        {
            PlacePiece(basePieces[0], row, col);
        }
        else if (row == ROWS - 2)   // White front row
        {
            PlacePiece(basePieces[6], row, col);
        }
        else if (row == ROWS - 1) // White back row
        {
            // Sequentially place back row units
            PlacePiece(basePieces[BackRow[col] + 6], row, col);
            // Only if king was placed, it will be marked
            IfKingAdd(WHITE, row, col);
        }
        else if (row == 0) // Black back row
        {
            PlacePiece(basePieces[BackRow[col]], row, col);
            IfKingAdd(BLACK, row, col);
        }
    }

    // If king is on square, add to kings list
    void IfKingAdd(int color, int row, int col)
    {
        if (Grid[row][col].PieceHeld.GetComponent<King>())
        {
            kings[color] = Grid[row][col].PieceHeld.GetComponent<King>();
            kings[color].x = col; kings[color].y = row;
            kingPlaced[color] = true;
        }
    }

    void RandomBackRowPlacement(int color, int kingNumber, int row, int col, int shift = 0)
    {
        int pieceNumber = Random.Range(0 + shift, 6 + shift); // [start, end)

        // If number corresponding to this color king chosen or at last column and king hasn't been placed
        if (pieceNumber == kingNumber || (col == COLS - 1 && !kingPlaced[color]))
        {
            // If king hasn't been placed, place it
            if (!kingPlaced[color])
            {
                PlacePiece(basePieces[kingNumber], row, col);
                // Store the king in kings array
                IfKingAdd(color, row, col);
            }
            else // else place knight
                PlacePiece(basePieces[2 + shift], row, col);
        }
        else  // else place piece
            PlacePiece(basePieces[pieceNumber], row, col);
    }

    // Handle setting team and adding to piece list
    void FinalizePiece(int row, int col)
    {
        // Set team to white. Default is already black.  White is -1. These numbers will also be used for movement direction.
        if (row >= ROWS - 2 && Grid[row][col].PieceHeld != null)
        {
            Grid[row][col].PieceHeld.GetComponent<Piece>().Team = -1;
        }
        if (Grid[row][col].PieceHeld)
        {
            var piece = Grid[row][col].PieceHeld;
            // Add pieces to list, except for kings.
            if (!piece.GetComponent<King>())
            {   // add to the appropriate team's piece list
                thePieces[row >= ROWS - 2 ? WHITE : BLACK].Add(piece);
                piece.GetComponent<Piece>().x = col; piece.GetComponent<Piece>().y = row;
            }
        }
    }

    public void HandleRevival(EventCallbacks.ReviveEvent e)
    {
        // Discard the pawn
        Grid[revivalPosY][revivalPosX].PieceHeld.transform.localPosition += new Vector3(0, -12, 0);

        // Move dead piece back onto the board
        Grid[revivalPosY][revivalPosX].PieceHeld = e.Piece;
        e.Piece.transform.localPosition = new Vector3(revivalPosX, 4, revivalPosY);
        e.Piece.transform.localEulerAngles = new Vector3(0, 0, 0);
        e.Piece.GetComponent<Piece>().x = revivalPosX;
        e.Piece.GetComponent<Piece>().y = revivalPosY;

        // Put zombies back to sleep
        new EventCallbacks.SleepEvent().FireEvent();
        Debug.Log("Handling revival, and just fired sleep event");

        // False this flag so Update can finish up the turn
        wokeFlag = false;
        revivedFlag = true;
    }
	
	private void Update () {
        //        if (Advertisement.IsReady())
        //          Advertisement.Show("video");

        if (reviveFlag || wokeFlag || revivedFlag)
        {
            // You need to revive a piece from your team.
            // A dead piece must exist, check the count
            // Only do this once block once, and then wait for a selection.
            if(reviveFlag && deadCount[teamTurn == 1 ? 0 : 1] != 0)
            {
                // Wake up your dead pieces
                var e = new EventCallbacks.WakeUpEvent();
                e.Team = teamTurn;
                e.FireEvent();
                wokeFlag = true;
                deadCount[teamTurn == 1 ? 0 : 1]--;
            }
            reviveFlag = false;

            // After revive, the event handler will flip this 
            // or if revive was skipped because it couldn't occur (no dead pieces), this flag would never have been true
            // Finish up.
            if (!wokeFlag)
            {
                // Check if enemy's team is now check mated.
                CheckMate();

                // If you check the other player's king, then they will have to move it next round
                Check();

                teamTurn *= -1; // Next turn will be other player.
                reviveFlag = false;
                wokeFlag = false;
                revivedFlag = false;

                // Wait for piece to drop before switching camera
                StartCoroutine("SwitchCam");
            }
        }
        else
        {
            UpdateSelection();

            // If left click
            if (Input.GetMouseButtonDown(0))
            {
                // If click on board
                if (selectionX >= 0 && selectionY >= 0 && selectionY < ROWS && selectionX < COLS)
                {
                    // Initial selection
                    if (selection == null)
                    {
                        // Select square
                        if (selectSquare())
                        {
                            // Lift chess piece
                            selectedPiece.GetComponent<Rigidbody>().isKinematic = true;
                            selectedPiece.transform.localPosition
                                = new Vector3(selection.transform.localPosition.x, 4, selection.transform.localPosition.z);
                            selectedPiece.transform.eulerAngles = new Vector3(0, 0, 0);

                            // We will need to know the initial position after a valid move is made.
                            selectionX1 = selectionX; selectionY1 = selectionY;


                            // Calculate all possible positions that the piece can move to
                            validLocations = selectedPiece.GetComponent<Piece>().CalculateLocations(ref Grid, selectionX, selectionY);
                            Dictionary<int, HashSet<int>> newValidLocations = new Dictionary<int, HashSet<int>>();
                            var isKing = selectedPiece.GetComponent<King>();

                            // Highlight all validLocations
                            if (Settings.HoverFlag)
                            {
                                teamTurn *=-1;
                                highLit = true;
                                foreach (var pair in validLocations)
                                {
                                    newValidLocations.Add(pair.Key, new HashSet<int>());
                                    var newSet = new HashSet<int>();
                                    newValidLocations.TryGetValue(pair.Key, out newSet);
                                    foreach (var Y in pair.Value)
                                    {
                                        // If checked, must decide if any of these moves are actually valid.
                                        // However, kings already check if their moves are truly valid
                                        if (checkedFlag && !isKing)
                                        {
                                            // Temporarily make the move
                                            var tempEnemy = Grid[Y][pair.Key].PieceHeld;
                                            Grid[Y][pair.Key].PieceHeld = selectedPiece;
                                            Grid[selectionY][selectionX].PieceHeld = null;

                                            // If still checked, this move is invalid
                                            bool stillChecked = Check();

                                            // Reverse move
                                            Grid[selectionY][selectionX].PieceHeld = selectedPiece;
                                            Grid[Y][pair.Key].PieceHeld = tempEnemy;

                                            // Skip this location if it is invalid
                                            if (stillChecked)
                                            {
                                                Debug.Log("Invalid move, can't highlight");
                                                continue;

                                            }
                                        }
                                        var rend = Grid[Y][pair.Key].Cube.GetComponent<Renderer>();
                                        var s = Shader.Find("Legacy Shaders/Self-Illumin/Diffuse");
                                        rend.material.shader = s;

                                        if (rend.material.name.Contains("Black"))
                                        {
                                            rend.material = a2;
                                        }
                                        else
                                        {
                                            rend.material = a4;
                                        }
                                        newSet.Add(Y);
                                    }
                                }
                                validLocations = newValidLocations;

                                teamTurn *= -1;
                            }


                        }
                        else // Couldn't select square. Wrong team?
                        {
                            selection = null;
                            // will have to try again, so, we will end up calling selectSquare until we pick a valid square
                        }
                    }
                    // A valid initial selection has been made. Now select where to move.
                    // We won't get here unless selection!=null and selectedPiece!=null
                    else
                    {
                        // Turn off halo
                        var halo = selection.GetComponent("Halo");
                        halo.GetType().GetProperty("enabled").SetValue(halo, false, null);

                        selectedPiece.GetComponent<Rigidbody>().isKinematic = false;

                        // Un-Highlight all validLocations
                        if (highLit)
                        {
                            highLit = false;
                            foreach (var pair in validLocations)
                            {
                                foreach (var Y in pair.Value)
                                {
                                    var rend = Grid[Y][pair.Key].Cube.GetComponent<Renderer>();
                                    var s = Shader.Find("Standard");


                                    if (rend.material.name.Contains("Black"))
                                    {
                                        rend.material = a1;
                                    }
                                    else
                                    {
                                        rend.material = a3;
                                    }
                                }
                            }
                        }

                        selection = null; // Let another selection after this

                        // Is this a valid destination?
                        HashSet<int> ySet;
                        if (validLocations.TryGetValue(selectionX, out ySet))
                        {
                            // The X coord was valid. Check the Y
                            if (ySet.Contains(selectionY) == false)
                            {
                                selectedPiece.GetComponent<Rigidbody>().useGravity = true;
                                // Did not find Y coord, invalid destination. Break here so another initial selection must be made, before switching teams
                                return;
                            }
                        }
                        else
                        {
                            selectedPiece.GetComponent<Rigidbody>().useGravity = true;
                            // Did not find X coord, invalid destination. Break here so another initial selection must be made, before switching teams
                            return;
                        }

                        /// At this point: A valid destination was selected. Time to move.

                        // Move chess piece
                        selectedPiece.transform.localPosition = new Vector3(selectionX, 3, selectionY);
                        selectedPiece.transform.eulerAngles = new Vector3(0, 0, 0);
                        selectedPiece.GetComponent<Piece>().x = selectionX;
                        selectedPiece.GetComponent<Piece>().y = selectionY;

                        if (selectedPiece.GetComponent<King>())
                        {
                            selectedPiece.GetComponent<King>().hasMoved = true;
                        }

                        checkedFlag = false; // If this flag was on, and we got here, we know it is not checked anymore

                        // Find enemy piece.
                        var enemy = Grid[selectionY][selectionX].PieceHeld;

                        // If there actually is an enemy there
                        if (enemy != null)
                        {
                            // Check castling case
                            // If this "enemy" is actually an allied Rook, we know this has to be a castling case
                            if (enemy.GetComponent<Rook>() && enemy.GetComponent<Piece>().Team == teamTurn)
                            {
                                Debug.Log("Rook special case");
                                int rightWard = -1;
                                // Although we highlighted the rook's position, we don't actually swap with the Rook
                                // King goes before Rook, then Rook moves before King
                                // Since we have a chance that the Rook is already initially adjacent to the King
                                // Rook will end up swapping with another piece if need be.
                                if (selectionX > selectionX1)
                                    rightWard = 1;

                                // Move king
                                Grid[selectionY][selectionX-rightWard].PieceHeld = selectedPiece;
                                selectedPiece.transform.localPosition = new Vector3(selectionX-rightWard, 3, selectionY);
                                selectedPiece.GetComponent<Piece>().x = selectionX-rightWard;
                                selectedPiece.GetComponent<Piece>().y = selectionY;
                                Grid[selectionY1][selectionX1].PieceHeld = null;

                                // Move rook
                                Grid[selectionY][selectionX-rightWard*2].PieceHeld = enemy;
                                enemy.transform.localPosition = new Vector3(selectionX-rightWard*2, 3, selectionY);
                                enemy.transform.eulerAngles = new Vector3(0, 0, 0);
                                enemy.GetComponent<Piece>().x = selectionX-rightWard*2;
                                enemy.GetComponent<Piece>().y = selectionY;
                                Grid[selectionY][selectionX].PieceHeld = null;
                                enemy.GetComponent<Rook>().hasMoved = true;

                                kings[teamTurn == 1 ? 0 : 1].x = selectionX-rightWard;
                            }
                            else
                            {
                                HandleKill(enemy); // Kill enemy
                                Grid[selectionY][selectionX].PieceHeld = selectedPiece; // selectedPiece moves to the new position
                                Grid[selectionY1][selectionX1].PieceHeld = null;
                            }
                        }
                        else
                        {
                            Grid[selectionY][selectionX].PieceHeld = selectedPiece; // selectedPiece moves to the new position
                            Grid[selectionY1][selectionX1].PieceHeld = null;
                        }

                        // Rook can't castle with king anymore
                        if (selectedPiece.GetComponent<Rook>()) selectedPiece.GetComponent<Rook>().hasMoved = true;

                        // Special cases for Pawn moves
                        if (Grid[selectionY][selectionX].PieceHeld)
                        {
                            var thePawn = Grid[selectionY][selectionX].PieceHeld.GetComponent<Pawn>();
                            if (thePawn != null)
                            {
                                thePawn.isInitial++;    // Count the pawn's moves

                                // A kill wasn't made, so maybe it was an En Passant capture
                                if (enemy == null)
                                {
                                    // Check En Passant condition. We may have made an En Passant capture.
                                    // To check En Passant, all we need to know is if there is an enemy pawn behind us and if we didn't make a kill on our move.
                                    // Because if we didn't make a kill, then the only way there can be a pawn behind us is if it was an En Passant diagonal move.
                                    // Use the teamTurn value to get to the cell behind. There is always a cell behind, bcuz we moved forward.
                                    var behind = Grid[selectionY - teamTurn][selectionX].PieceHeld;
                                    if (behind != null)
                                    {
                                        var pawnBehind = behind.GetComponent<Pawn>();
                                        // If it is an enemy pawn
                                        if (pawnBehind != null && pawnBehind.Team != teamTurn)
                                        {
                                            /// It was an En Passant capture. We need to kill the enemy.
                                            HandleKill(behind);
                                        }
                                    }
                                }
                                // Check revive case -- Pawn has reached opposing end.
                                if ((teamTurn == 1 && selectionY == ROWS - 1) || (teamTurn == -1 && selectionY == 0))
                                {
                                    // Flip revive flag, now we have to poll for revival before we can do anything else with the board.
                                    reviveFlag = true;
                                    revivalPosX = selectionX; revivalPosY = selectionY;
                                    // So we break here, then once revival occurs we will finish up and switch turn
                                    return;
                                }
                            }
                        }
                        // Check pawn swapping dead case.
                        //How could we possibly allow the user to swap dead here?
                        //They need to make a selection, but we are here.
                        // Maybe we neeed to move all this business to an async method, and we await something?
                        // Or maybe we have to leave this method, set some flag. 
                        // And, while the flag is set, we don't come back in here anymore.
                        // We wait until a dead piece is clicked on (if there are no dead pieces, well pawn loses his chance)
                        // Once dead piece is clicked on, we need to flip the flag. So, how does pawn notify the board?
                        // Pawn would need to be observing the board. Or the board needs to be subscribed to pawn events.




                        // At this point: selectedPiece has been moved to a valid location




                        // We should stop the game if checkmate or stalemate. Also should notify if mate.
                        // Stale mate occurs if current player has no valid moves, but isn't in check.
                        // There doesn't seem to be a sensible performant way to check for stalemate.  Think of a big board
                        // If player has no moves, he will have to give up. Or maybe he can ask to check stalemate condition.

                        // Only check if enemy's team is now check mated, because you can't move your own king into check mate on your turn.
                        CheckMate();

                        // If you check the other player's king, then they will have to move it next round
                        Check();

                        teamTurn *= -1; // Next turn will be other player.

                        // Wait for piece to drop before switching camera
                        StartCoroutine("SwitchCam");
                    }
                }
            }
        }

    }

    private void HandleKill(GameObject enemy)
    {
        // For white's turn, we are killing a black, and putting him in the black bin
        if (teamTurn == -1)
            enemy.transform.localPosition = new Vector3(-6.7f - (deadCount[teamTurn == -1 ? 1 : 0]*.01f), 2.8f + (deadCount[teamTurn == -1 ? 1 : 0] * .08f), 0);
        else
            enemy.transform.localPosition = new Vector3(-6.7f - (deadCount[teamTurn == -1 ? 1 : 0] * .01f), 2.8f+ (deadCount[teamTurn == -1 ? 1 : 0] * .08f), ROWS);

        // Increment opposing team's deadCount
        deadCount[teamTurn == -1 ? 0 : 1]++;

        // Notify piece it is dead so it can start accepting clicks. It also subscribes to the WakeUpEvent
        enemy.GetComponent<Piece>().Die();


        // Nope
        // Enemy needs to go in a dead collection for his team
        //deadPieces[teamTurn==1 ? 0 : 1].Add

    }

    IEnumerator AnimateWinText(GameObject text)
    {

         var rot = text.GetComponent<RectTransform>();
        int i = 1, j = 1, k = 1;
        for (; i < 40; ++i)
        {
            rot.Rotate(new Vector3(i,i,i*-1));
            yield return new WaitForSecondsRealtime(0.0001f);
        }

        for (; i >=0; --i)
        {
            rot.Rotate(new Vector3(i*-1, i*-1, i));
            yield return new WaitForSecondsRealtime(0.01f);
        }
        rot.eulerAngles = new Vector3(0, 0, 0);
        yield return new WaitForSecondsRealtime(1.7f);
        gameOver.SetActive(true);

        yield return null;
    }
    private void CheckMate()
    {
        if (teamTurn == 1) // Black's turn
        {
            // Call white King's CheckMate(), passing in white King's coords
            if (kings[1].CheckMate(ref Grid, kings[1].x, kings[1].y))
            {
                // We only know that the King can't move.
                // Now, we need to know if any white piece can get them out of check.
                // So, iterate each alive piece and calculate valid locations
                    // Iterate valid locations and try to see if the move removes the check.
                foreach(var p in thePieces[1])
                {
                    var piece = p.GetComponent<Piece>();
                    // If piece is alive
                    if (!piece.IsDead)
                    {
                        var moves = piece.CalculateLocations(ref Grid, piece.x, piece.y);
                        foreach (var pair in moves)
                        {
                            foreach (var Y in pair.Value)
                            {
                                // Temporarily make the move
                                var tempEnemy = Grid[Y][pair.Key].PieceHeld;
                                Grid[Y][pair.Key].PieceHeld = p;
                                Grid[piece.y][piece.x].PieceHeld = null;

                                // If still checked, this move is invalid.
                                bool stillChecked = Check();

                                // Reverse move
                                Grid[piece.y][piece.x].PieceHeld = p;
                                Grid[Y][pair.Key].PieceHeld = tempEnemy;

                                // We've found a piece with a valid move (unchecking move), so we aren't check mated.
                                if (!stillChecked)
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
                // Black wins!
                blackWins.SetActive(true);
                StartCoroutine(AnimateWinText(blackWins));
            }
        }
        else
        {
            if (kings[0].CheckMate(ref Grid, kings[0].x, kings[0].y))
            {
                foreach (var p in thePieces[0])
                {
                    var piece = p.GetComponent<Piece>();
                    // If piece is alive
                    if (!piece.IsDead)
                    {
                        var moves = piece.CalculateLocations(ref Grid, piece.x, piece.y);
                        foreach (var pair in moves)
                        {
                            foreach (var Y in pair.Value)
                            {
                                // Temporarily make the move
                                var tempEnemy = Grid[Y][pair.Key].PieceHeld;
                                Grid[Y][pair.Key].PieceHeld = p;
                                Grid[piece.y][piece.x].PieceHeld = null;

                                // If still checked, this move is invalid.
                                bool stillChecked = Check();

                                // Reverse move
                                Grid[piece.y][piece.x].PieceHeld = p;
                                Grid[Y][pair.Key].PieceHeld = tempEnemy;

                                // We've found a piece with a valid move (unchecking move), so we aren't check mated.
                                if (!stillChecked)
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
                // White wins!
                whiteWins.SetActive(true);
                StartCoroutine(AnimateWinText(whiteWins));
            }
        }
    }

    private bool Check()
    {
        if (teamTurn == 1) // Black's turn
        {
            // Call white King's Check(), passing in white King's coords
            if (kings[1].Check(ref Grid, kings[1].x, kings[1].y))
            {
                // White king is checked
                checkedFlag = true;
                Debug.Log("White king is checked");
                return true;
            }
        }
        else
        {
            if (kings[0].Check(ref Grid, kings[0].x, kings[0].y))
            {
                checkedFlag = true;
                Debug.Log("Black king is checked");
                return true;
            }
        }
        return false;
    }

    // Switches camera after delay
    IEnumerator SwitchCam()
    {
        yield return new WaitForSeconds(1.2f);        
        
        // Switch camera orientation
        cam.transform.localPosition = camDefaultPos;
        cam.transform.eulerAngles = camDefaultRot;
        if (teamTurn == -1)
        {
            cam.transform.eulerAngles += new Vector3(0, 180, 0);
            cam.transform.localPosition += new Vector3(0, 0, ROWS + 11);
        }
    }

    // Track mouse, and update which square is selected
    // Note: the center of the first square is at 0,0. it extends .5 in four XZ directions. So we truncate it
    // So now first square is from 0-1 on X and Z. So, let's say 1,1 actually starts the diagonal square
    private void UpdateSelection()
    {
        if (!Camera.main) return;

        RaycastHit hit;
        if (Physics.Raycast(
            Camera.main.ScreenPointToRay(Input.mousePosition), 
            out hit, 50.0f, LayerMask.GetMask("Plane")))
        {
            //Debug.Log(hit.point);
            selectionX = (int)(hit.point.x+0.5f);
            selectionY = (int)(hit.point.z+0.5f);
        }
        else
        {
            selectionX = selectionY = -1;
        }
    }

    // Ensure validity of the initial square selection
    private bool selectSquare()
    {
        selectedPiece = Grid[selectionY][selectionX].PieceHeld;
        // If empty square, return false
        if (selectedPiece == null) return false;

        // If square of wrong team selected, return false
        if (selectedPiece.GetComponent<Piece>().Team != teamTurn) return false;

        // Determine whether if this piece leaves his square will cause the king to be checked
        if (!checkedFlag && selectedPiece.GetComponent<King>()==null)
        {
            // Temporarily remove the piece
            Grid[selectionY][selectionX].PieceHeld = null;

            // Check if the king is checked now
            if ((teamTurn == 1 && kings[0].Check(ref Grid, kings[0].x, kings[0].y))
                || (teamTurn == -1 && kings[1].Check(ref Grid, kings[1].x, kings[1].y)))
            {
                // Reject this selection, can't move this piece if it will check the king
                Grid[selectionY][selectionX].PieceHeld = selectedPiece;
                return false;
            }

            Grid[selectionY][selectionX].PieceHeld = selectedPiece;
        }

        // Select cube
        selection = Grid[selectionY][selectionX].Cube;

        // Turn on halo
        var halo = selection.GetComponent("Halo");
        halo.GetType().GetProperty("enabled").SetValue(halo, true, null);
        return true;

    }

}

// find first game object in scene of given type
//FindObjectOfType<Square>()