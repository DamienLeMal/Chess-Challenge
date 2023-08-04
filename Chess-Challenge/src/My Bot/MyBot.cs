﻿using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using static System.Formats.Asn1.AsnWriter;

public class MyBot : IChessBot {
    int[] pieceValues = { 0, 100, 300, 310, 500, 900, 10000 };
    Move bestMove;
    int maxDepth = 50;
    int maxTime = 100;
    int totalTime = 0;
    public Move Think (Board board, Timer timer)
    {

        bestMove = Move.NullMove;
        Move[] moves = board.GetLegalMoves();
        

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
            //Console.WriteLine(i - 1);
            if (timer.MillisecondsElapsedThisTurn >= maxTime)
                break;
        }
        totalTime += timer.MillisecondsElapsedThisTurn;
        int average = totalTime / (board.PlyCount/2+1);
        Console.WriteLine("Average speed = " + average);
        return transpositionsTable[board.ZobristKey & tpMask].BestMove;
    }
    int bestMoveChanged = 0;
    int EvaluateBoard (Board board, Timer timer, int depth, int color, int alpha, int beta, int ply = 0) {

        bool notRoot = ply > 0;
        int maxScore = -999999;
        int startingAlpha = alpha;

        ref Transposition transposition = ref transpositionsTable[board.ZobristKey & tpMask];

        if (notRoot && board.IsRepeatedPosition()) return 0;

        //Ordering moves

        Move[] movesBestFirst = board.GetLegalMoves();
        int[] movePriorityTable = new int[movesBestFirst.Length];

        for (int i = 0; i < movesBestFirst.Length; i++) {
            if (movesBestFirst[i] == transposition.BestMove)
                movePriorityTable[i] = 1;
            else
                movePriorityTable[i] = 999999;
        }

        //Need work to avoid draw by repetition
        if (notRoot && transposition.ZHash == board.ZobristKey && transposition.Depth > depth && (
            transposition.Flag == 3 // exact score
            || transposition.Flag == 2 && transposition.Eval >= beta // lower bound, fail high
            || transposition.Flag == 1 && transposition.Eval <= alpha // upper bound, fail low
        )) return transposition.Eval;

        if (depth == 0 || board.GetLegalMoves().Length == 0)
            return GetBoardScore(board, color);

        foreach (Move m in movesBestFirst) {

            if (timer.MillisecondsElapsedThisTurn >= maxTime) return 999999;

            board.MakeMove(m);
            int score = -EvaluateBoard(board, timer, depth-1, -color, -beta, -alpha, ply+1);
            board.UndoMove(m);

            if (score > maxScore) {
                maxScore = score;
                if (ply == 0 && bestMove != m)//only change if m is a legal move (undo move would result in current board)
                    { bestMoveChanged++; bestMove = m;  }
            }
            alpha = Math.Max(alpha, maxScore);
            if (alpha >= beta) break;
        }

        int bound = maxScore >= beta ? 2 : maxScore > startingAlpha ? 3 : 1;
        //Add to Transposition Table
        transpositionsTable[board.ZobristKey & tpMask] = new Transposition(board.ZobristKey, maxScore, bestMove, (sbyte)depth, (sbyte)bound);

        return maxScore;
    }

    int GetBoardScore (Board b, int color) {
        if (b.IsInCheckmate()) return -999999;
        if (b.IsDraw() || b.IsRepeatedPosition()) return 0;

        int score = 0;
        for (int i = 0; ++i < 7;)
            score += (b.GetPieceList((PieceType)i, true).Count - b.GetPieceList((PieceType)i, false).Count) * pieceValues[i];

        score *= color;

        return score;
    }


    const ulong tpMask = 0x7FFFFF;
    Transposition[] transpositionsTable = new Transposition[tpMask + 1];

}

struct Transposition {
    public ulong ZHash;
    public int Eval;
    public Move BestMove;
    public sbyte Depth, Flag;

    public Transposition(ulong zHash, int eval, Move bestMove, sbyte depth, sbyte flag) {
        ZHash = zHash;
        Eval = eval;
        BestMove = bestMove;
        Depth = depth;
        Flag = flag;
    }
}