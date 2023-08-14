using ChessChallenge.API;
using ChessChallenge.Application;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using static System.Formats.Asn1.AsnWriter;

public class Test : IChessBot {
    const ulong tpMask = 0x7FFFFF;
    Transposition[] transpositionsTable = new Transposition[tpMask + 1];
    Move bestMoveRoot;
    Timer clock;
    int node = 0, qnode = 0; //#DEBUG
    float maxTimePerTurn;
    bool OutOfTime => clock.MillisecondsElapsedThisTurn > maxTimePerTurn;
    public Move Think(Board board, Timer timer) {
        node = qnode = 0;// #DEBUG
        clock = timer;
        maxTimePerTurn = clock.MillisecondsRemaining / 40;

        Move[] moves = board.GetLegalMoves();

        //stop if checkmate or one move possible
        if (moves.Length == 1) return moves[0];
        foreach (Move m in moves) {
            board.MakeMove(m);
            if (board.IsInCheckmate()) return m;
            board.UndoMove(m);
        }
        bestMoveRoot = moves[0];
        for (int depth = 1; ;) {
            Negamax(board, timer, ++depth, -999999, 999999, 0);
            //Console.WriteLine("My Bot hit depth: " + depth + " in " + clock.MillisecondsElapsedThisTurn + " with best " + bestMoveRoot);// #DEBUG
            //Console.WriteLine("My Bot hit depth: " + depth + " in " + clock.MillisecondsElapsedThisTurn + "ms with an eval of " + // #DEBUG
            //    transpositionsTable[board.ZobristKey & 0x3FFFFF].Eval + " centipawns"); // #DEBUG
            if (OutOfTime) return ReturnFunction(bestMoveRoot);
        }
    }

    Move ReturnFunction(Move function) {
        //Console.WriteLine("Node : " + node);//#DEBUG
        //Console.WriteLine("QNode : " + qnode);//#DEBUG
        //Console.WriteLine("Total : " + (node + qnode));//#DEBUG
        return function;

    }

    // alpha is minimum score assured after full analysis
    // beta is maximum score assured after full analysis

    int Negamax(Board board, Timer timer, int depth, int alpha, int beta, int ply) {
        bool notRoot = ply > 0;
        bool qsearch = depth <= 0;
        int maxScore = -999999;
        int startingAlpha = alpha;
        ulong zobristKey = board.ZobristKey;

        Move bestMove = Move.NullMove;//Keep track of best move for current depth board step

        if (notRoot && board.IsRepeatedPosition()) return 0;// before TT to avoid transposition loop

        ref Transposition transposition = ref transpositionsTable[zobristKey & tpMask];

        if (notRoot && transposition.ZHash == zobristKey && transposition.Depth > depth) {

            int ttEval = transposition.Eval;//token count : 3 use => same, more uses and it's worth
            int ttFlag = transposition.Flag;

            if (ttFlag == 2) //Lower bound
                alpha = Math.Max(alpha, ttEval);
            else if (ttFlag == 1) //Upper bound
                beta = Math.Min(beta, ttEval);
            if (alpha >= beta || ttFlag == 1)
                return ttEval;
        }

        //Standing pat
        if (qsearch) {//final depth
            qnode++; // #DEBUG
            maxScore = GetBoardScore(board);

            //Delta pruning
            if (board.GetAllPieceLists().Length >= 10 && board.GameMoveHistory.Length > 1) //Not used in endgame
                if (maxScore < alpha - (975 + (board.GameMoveHistory.Last().IsPromotion ? 775 : 0))) return alpha;

            if (maxScore >= beta) return maxScore;//if maxscore is better than beta, position is quiet
            alpha = Math.Max(alpha, maxScore);
        } else node++;//#DEBUG


        //Get moves
        Move[] movesBestFirst = board.GetLegalMoves(qsearch && !board.IsInCheck());//check only for capture in qsearch exept if in check, to also search evading moves
        int[] movePriorityTable = new int[movesBestFirst.Length];


        //No available move
        if (!qsearch && movesBestFirst.Length == 0)
            return board.IsInCheck() ?
                -999999 + ply   // checkmate, -ply so further mate are less important than close ones
                : 0;    //stalemate


        //Ordering moves

        for (int i = 0; i < movesBestFirst.Length; i++) {
            Move m = movesBestFirst[i];

            movePriorityTable[i] = m == transposition.BestMove ? 999999 :
                m.IsPromotion ? 888888 :
                m.IsCapture ? 1000 * (int)m.CapturePieceType - (int)m.MovePieceType : 0;
        }

        Array.Sort(movePriorityTable, movesBestFirst);

        Array.Reverse(movesBestFirst);

        foreach (Move m in movesBestFirst) {
            if (OutOfTime)
                return 999999;

            board.MakeMove(m);
            int score = -Negamax(board, timer, depth - 1, -beta, -alpha, ply + 1);
            board.UndoMove(m);

            if (score > maxScore) {
                maxScore = score;
                bestMove = m;

                if (ply == 0)  //only change if m is a legal move (undo move would result in current board)
                    bestMoveRoot = m;
                alpha = Math.Max(alpha, maxScore);
                if (alpha >= beta) {
                    break;// if minimum score assured is better than maximum score assured no need to search further thanks to move ordering
                }
            }
        }


        //Add to Transposition Table                                                                                          Bound
        transpositionsTable[zobristKey & tpMask] = new Transposition(
            zobristKey,
            maxScore, bestMove,
            (sbyte)depth,
            (sbyte)(maxScore >= beta ? 2 : maxScore > startingAlpha ? 3 : 1));

        return maxScore;
    }

    int GetBoardScore(Board b) {
        int middlegame = 0, endgame = 0, gamephase = 0;
        foreach (bool sideToMove in new[] { true, false }) {
            for (int piece = -1, square; ++piece < 6;)
                for (ulong mask = b.GetPieceBitboard((PieceType)piece + 1, sideToMove); mask != 0;) {
                    // Gamephase, middlegame -> endgame
                    gamephase += GamePhaseIncrement[piece];

                    // Material and square evaluation
                    square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (sideToMove ? 56 : 0);
                    middlegame += UnpackedPestoTables[square][piece];
                    endgame += UnpackedPestoTables[square][piece + 6];
                }

            middlegame = -middlegame;
            endgame = -endgame;
        }
        return (middlegame * gamephase + endgame * (24 - gamephase)) / 24 * (b.IsWhiteToMove ? 1 : -1);
    }

    private readonly int[] GamePhaseIncrement = { 0, 1, 1, 2, 4, 0 };
    // None, Pawn, Knight, Bishop, Rook, Queen, King 
    private readonly short[] PieceValues = { 82, 337, 365, 477, 1025, 0, // Middlegame
                                                94, 281, 297, 512, 936, 0}; // Endgame

    // Big table packed with data from premade piece square tables
    // Unpack using PackedEvaluationTables[set, rank] = file
    private readonly decimal[] PackedPestoTables = {
            63746705523041458768562654720m, 71818693703096985528394040064m, 75532537544690978830456252672m, 75536154932036771593352371712m, 76774085526445040292133284352m, 3110608541636285947269332480m, 936945638387574698250991104m, 75531285965747665584902616832m,
            77047302762000299964198997571m, 3730792265775293618620982364m, 3121489077029470166123295018m, 3747712412930601838683035969m, 3763381335243474116535455791m, 8067176012614548496052660822m, 4977175895537975520060507415m, 2475894077091727551177487608m,
            2458978764687427073924784380m, 3718684080556872886692423941m, 4959037324412353051075877138m, 3135972447545098299460234261m, 4371494653131335197311645996m, 9624249097030609585804826662m, 9301461106541282841985626641m, 2793818196182115168911564530m,
            77683174186957799541255830262m, 4660418590176711545920359433m, 4971145620211324499469864196m, 5608211711321183125202150414m, 5617883191736004891949734160m, 7150801075091790966455611144m, 5619082524459738931006868492m, 649197923531967450704711664m,
            75809334407291469990832437230m, 78322691297526401047122740223m, 4348529951871323093202439165m, 4990460191572192980035045640m, 5597312470813537077508379404m, 4980755617409140165251173636m, 1890741055734852330174483975m, 76772801025035254361275759599m,
            75502243563200070682362835182m, 78896921543467230670583692029m, 2489164206166677455700101373m, 4338830174078735659125311481m, 4960199192571758553533648130m, 3420013420025511569771334658m, 1557077491473974933188251927m, 77376040767919248347203368440m,
            73949978050619586491881614568m, 77043619187199676893167803647m, 1212557245150259869494540530m, 3081561358716686153294085872m, 3392217589357453836837847030m, 1219782446916489227407330320m, 78580145051212187267589731866m, 75798434925965430405537592305m,
            68369566912511282590874449920m, 72396532057599326246617936384m, 75186737388538008131054524416m, 77027917484951889231108827392m, 73655004947793353634062267392m, 76417372019396591550492896512m, 74568981255592060493492515584m, 70529879645288096380279255040m,
    };

    private int[][] UnpackedPestoTables;

    // Constructor unpacks the tables and "bakes in" the piece values to use in your evaluation
    public Test() {
        UnpackedPestoTables = new int[64][];
        UnpackedPestoTables = PackedPestoTables.Select(packedTable => {
            int pieceType = 0;
            return decimal.GetBits(packedTable).Take(3)
                .SelectMany(c => BitConverter.GetBytes(c)
                    .Select((byte square) => (int)((sbyte)square * 1.461) + PieceValues[pieceType++]))
                .ToArray();
        }).ToArray();
        /*
        // print uncompressed table
        for (int j = 0; j < 12; j++) {//Piece
            Console.WriteLine("\nPiece type : " + (PieceType)((j+1)%6));
            for (int i = 1; i <= 64; i++) {//Square
                int value = PieceValues[j];
                string s = (new Square(i-1)).Name;
                Console.Write(s + " " + (UnpackedPestoTables[i-1][j] - value) + "  ");
                if (i % 8 == 0) Console.WriteLine("\n");
            }
        }*/
    }

    struct Transposition {
        public ulong ZHash;
        public int Eval;
        public sbyte Depth, Flag;
        public Move BestMove;

        public Transposition(ulong zHash, int eval, Move bestMove, sbyte depth, sbyte flag) {
            ZHash = zHash;
            Eval = eval;
            BestMove = bestMove;
            Depth = depth;
            Flag = flag;
        }
    }
}

