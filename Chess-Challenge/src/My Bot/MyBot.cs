using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using static System.Formats.Asn1.AsnWriter;

public class MyBot : IChessBot {
    int[] pieceValues = { 0, 100, 300, 310, 500, 900, 10000 };
    Move bestMove;
    int maxDepth = 10;
    Timer myTimer;
    public Move Think (Board board, Timer timer)
    {
        myTimer = timer;
        bestMove = Move.NullMove;

        for (int i = 1; i <= maxDepth; i++) {
            int score = EvaluateBoard(board, i, board.IsWhiteToMove ? 1 : -1, -999999, 999999, 0);
            Console.WriteLine("Finnished depth " + i + " in " + timer.MillisecondsElapsedThisTurn);
            if (timer.MillisecondsElapsedThisTurn >= 10000) {
                //Console.WriteLine("Time's up");
                break;
            }
        }

        Console.WriteLine(timer.MillisecondsElapsedThisTurn + " ms");
        if (bestMove == Move.NullMove) {
            bestMove = board.GetLegalMoves()[0];
            Console.WriteLine("illegal move");
        }
        return bestMove;
    }

    int EvaluateBoard (Board board, int depth, int color, int alpha, int beta, int ply = 0) {



        ref Transposition transposition = ref transpositionsTable[board.ZobristKey & tpMask];

        int maxScore = int.MinValue;

        if (depth == 0 || board.GetLegalMoves().Length == 0)
            return GetBoardScore(board, color);

        if (transposition.ZHash == board.ZobristKey && transposition.Depth > depth) {
            return transposition.Eval;
        }


        foreach (Move m in board.GetLegalMoves()) {
            board.MakeMove(m);

            ref Transposition t = ref transpositionsTable[board.ZobristKey & tpMask];

            


            int score = -EvaluateBoard(board,depth-1, -color, -alpha, -beta, ply+1);

            if (depth > t.Depth) {
                t.Depth = depth;
                t.Eval = score;
                t.ZHash = board.ZobristKey;
                t.Flag = UPPERBOUND;
            }


            if (score > maxScore) {
                maxScore = score;
                if (ply == 0)//only change if m is a legal move (undo move would result in current board)
                    bestMove = m;
            }
            alpha = Math.Max(alpha, maxScore);
            if (alpha >= beta) break;

            board.UndoMove(m);

        }

        return maxScore;
    }

    int GetBoardScore (Board b, int color) {
        if (b.IsInCheckmate()) return -999999;
        if (b.IsDraw()) return 0;
        if (b.IsRepeatedPosition()) return 0;

        int score = 0;
        for (int i = 0; ++i < 7;)
            score += (b.GetPieceList((PieceType)i, true).Count - b.GetPieceList((PieceType)i, false).Count) * pieceValues[i];

        score *= color;


        return score;
    }


    const ulong tpMask = 0x7FFFFF;
    sbyte INVALID = 0, LOWERBOUND = 1, UPPERBOUND = 2, EXACT = 3;
    Transposition[] transpositionsTable = new Transposition[tpMask + 1];

}

struct Transposition {
    public ulong ZHash;
    public int Eval;
    public Move BestMove;
    public int Depth;
    public sbyte Flag;
}