namespace Chess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class MCTSNode
    {
        public Board state;
        public MCTSNode parent;
        public List<MCTSNode> children = new List<MCTSNode>();
        public List<Move> allPossibleMoves = new List<Move>();
        public int lastExpandedMoveIdx = -1;
        public Move initialMove;    // The initial move that lead to this state 
        public int numVisits = 0;
        public float numWins = 0;
        public float UCTValue = 0;
        public bool isEnemyTurn;

        public MCTSNode(Board board, Move initialMove, bool isEnemyTurn)
        {
            this.state = board;
            this.initialMove = initialMove;
            this.isEnemyTurn = isEnemyTurn;
        }

        public void Backpropagate(float result)
        {
            numVisits++;
            numWins += isEnemyTurn? -result : result;

            if (parent != null)
            {
                parent.Backpropagate(result);
            }
        }

        public MCTSNode SelectBestChild()
        {
            return children.OrderByDescending(child => child.UCTValue).FirstOrDefault();
        }
    }
}