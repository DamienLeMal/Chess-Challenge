using ChessChallenge.API;
using System;

public class EvilBot : IChessBot {
    int[] pieceValues = { 0, 100, 300, 310, 500, 900, 10000 };
    Move bestMove;
    int bestScore;
    int maxDepth = 10;
    int maxTime = 1000;
    int totalTime = 0;
    int numberOdNodes;
    public Move Think(Board board, Timer timer) {
        numberOdNodes = 0;
        bestMove = Move.NullMove;
        Move[] moves = board.GetLegalMoves();
        maxTime = timer.MillisecondsRemaining / 30;


        //stop if checkmate or one move possible
        if (moves.Length == 1) return moves[0];
        foreach (Move m in moves) {
            board.MakeMove(m);
            if (board.IsInCheckmate()) return m;
            board.UndoMove(m);
        }

        for (int i = 1; i <= maxDepth; i++) {
            bestMoveChanged = 0;
            EvaluateBoard(board, timer, i, board.IsWhiteToMove ? 1 : -1, -999999, 999999, 0);
            //Console.WriteLine(bestMoveChanged);
            if (timer.MillisecondsElapsedThisTurn >= maxTime)
                break;
        }
        totalTime += timer.MillisecondsElapsedThisTurn;
        int average = totalTime / (board.PlyCount / 2 + 1);
        //Console.WriteLine("Average speed = " + average + " max depth : " + transpositionsTable[board.ZobristKey & tpMask].Depth);
        //Console.WriteLine("Number of nodes : " + numberOdNodes);

        Move move = transpositionsTable[board.ZobristKey & tpMask].BestMove;
        if (move == Move.NullMove) {
            move = bestMove;
            if (move == Move.NullMove) {
                //Console.WriteLine("NULL MOVE " + bestMoveChanged);
                move = board.GetLegalMoves()[0];
            } else {

                //Console.WriteLine("BEST MOVE");
            }
        }
        //Console.WriteLine("Move played is " + move + " with score : " + transpositionsTable[board.ZobristKey & tpMask].Eval);
        return move;
    }
    int bestMoveChanged = 0;
    int EvaluateBoard(Board board, Timer timer, int depth, int color, int alpha, int beta, int ply = 0) {

        bool notRoot = ply > 0;
        int maxScore = -999999;
        int startingAlpha = alpha;
        bool qSearch = depth <= 0;//when at the end 

        if (notRoot && board.IsRepeatedPosition()) return 0;

        ref Transposition transposition = ref transpositionsTable[board.ZobristKey & tpMask];

        //Need work to avoid draw by repetition
        if (notRoot && transposition.ZHash == board.ZobristKey && transposition.Depth > depth && (
            transposition.Flag == 3 // exact score
            || transposition.Flag == 2 && transposition.Eval >= beta // lower bound, fail high
            || transposition.Flag == 1 && transposition.Eval <= alpha // upper bound, fail low
        )) return transposition.Eval;

        if (depth == 0 || board.GetLegalMoves().Length == 0)
            return GetBoardScore(board, color);



        //Ordering moves

        Move[] movesBestFirst = board.GetLegalMoves(qSearch);
        int[] movePriorityTable = new int[movesBestFirst.Length];

        for (int i = 0; i < movesBestFirst.Length; i++) {
            if (movesBestFirst[i] == transposition.BestMove)
                movePriorityTable[i] = 1;
            else//heuristic
                movePriorityTable[i] = 999999 - (BitboardHelper.GetNumberOfSetBits(board.WhitePiecesBitboard) - BitboardHelper.GetNumberOfSetBits(board.BlackPiecesBitboard));
        }


        foreach (Move m in movesBestFirst) {

            if (timer.MillisecondsElapsedThisTurn >= maxTime) {
                //Console.WriteLine(bestMove + " with score " + bestScore);
                return 999999;
            }
            numberOdNodes++;
            board.MakeMove(m);
            int score = -EvaluateBoard(board, timer, depth - 1, -color, -beta, -alpha, ply + 1);
            board.UndoMove(m);

            if (score > maxScore) {
                maxScore = score;
                if (ply == 0 && bestMove != m)//only change if m is a legal move (undo move would result in current board)
                    { bestMoveChanged++; bestMove = m; bestScore = maxScore; }
                alpha = Math.Max(alpha, maxScore);
                if (alpha >= beta) break;
            }
        }

        //Check for mate 
        if (movesBestFirst.Length == 0 && !qSearch) return board.IsInCheck() ? -999999 + ply : 0;


        int bound = maxScore >= beta ? 2 : maxScore > startingAlpha ? 3 : 1;
        //Add to Transposition Table
        transpositionsTable[board.ZobristKey & tpMask] = new Transposition(board.ZobristKey, maxScore, bestMove, (sbyte)depth, (sbyte)bound);

        //Console.WriteLine(bestMove + " with end score " + bestScore);
        return maxScore;
    }

    int GetBoardScore(Board b, int color) {
        if (b.IsInCheckmate()) return -999999;
        if (b.IsDraw() || b.IsRepeatedPosition()) return 0;

        int score = 0;
        for (int i = 0; ++i < 7;)
            score += (b.GetPieceList((PieceType)i, true).Count - b.GetPieceList((PieceType)i, false).Count) * pieceValues[i];

        score *= color;
        //score += GetControllScore(b, color);

        return score;
    }

    int GetControllScore(Board board, int color) {
        int score = 0;
        foreach (PieceList pieces in board.GetAllPieceLists()) {
            foreach (Piece p in pieces) {
                int count = 0;
                switch (p.PieceType) {
                    case PieceType.Pawn:
                        //ulong b = board.GetPieceBitboard(PieceType.Pawn, p.IsWhite);
                        //int i = BitboardHelper.ClearAndGetIndexOfLSB(ref b)/8;
                        //count = (10 - (p.IsWhite ? 7-i : i)) * 10 * (board.PlyCount > 40 ? 1 :0);
                        //Console.WriteLine((p.IsWhite ? "White" : "Black") +  " pawn in " + p.Square.Name + " is " + count + " square from promotion");
                        break;
                    case PieceType.Knight:
                        count = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetKnightAttacks(p.Square));
                        break;
                    case PieceType.Bishop:
                    case PieceType.Rook:
                    case PieceType.Queen:
                        count = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetSliderAttacks(p.PieceType, p.Square, board));
                        break;
                    case PieceType.King:
                        //count = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetKingAttacks(p.Square));
                        break;
                }
                score += count * pieceValues[(int)p.PieceType] / 100 * color;
            }
        }
        //Console.WriteLine(score);
        return score;
    }


    const ulong tpMask = 0x7FFFFF;
    Transposition[] transpositionsTable = new Transposition[tpMask + 1];
}