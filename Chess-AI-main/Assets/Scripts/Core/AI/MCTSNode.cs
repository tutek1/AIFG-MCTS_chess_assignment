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
        public float numVisits = 0;
        public float sumValue = 0;
        public int depth = 0;
        public bool isEnemyTurn;

        const float C = 1f;

        public MCTSNode(MCTSNode parent, Board board, Move initialMoveIdx)
        {
            this.state = board;
            this.parent = parent;
            this.initialMove = initialMoveIdx;
            if (parent != null) this.isEnemyTurn = !parent.isEnemyTurn;
            else this.isEnemyTurn = false;
            if (parent != null) this.depth = parent.depth + 1;
        }

        public void Backpropagate(MCTSSearch.SimResult result)
        {
            numVisits++;
            float value = state.WhiteToMove? result.whiteValue : result.blackValue;

            sumValue += value;
            
            if (parent != null)
            {
                parent.Backpropagate(result);
            }
        }

        public MCTSNode SelectBestChild()
        {
            if (children.Count == 0) return this;
            
            // Choose myself as the best child to expand if all children were not yet tried atleast once (=> created)
            if (allPossibleMoves.Count != children.Count)
            {
                return this;
            }

            // Get the best child based on UCT
            float bestValue = children[0].GetUCTValue();
            MCTSNode bestChild = children[0];
            foreach (MCTSNode child in children)
            {
                if (child.GetUCTValue() > bestValue)
                {
                    bestValue = child.GetUCTValue();
                    bestChild = child;
                }
            }

            return bestChild;
        }

        // Policy to select the best move -> sumValue of simulations
        public Move SelectBestMove()    
        {
            Move bestMove = children[0].initialMove;
            float bestValue = children[0].sumValue;
            foreach (MCTSNode node in children)
            {
                if (node.sumValue > bestValue)
                {
                    bestMove = node.initialMove;
                    bestValue = node.sumValue;
                }
            }

            return bestMove;
        }

        // Calculates and returns the UCT value for the node
        public float GetUCTValue()
        {
            if (parent == null) return 0;               // Root node case
            if (numVisits == 0) return Mathf.Infinity;  // Fresh node case -> uct limit goes to inf

            return sumValue/numVisits + C * Mathf.Sqrt(Mathf.Log(parent.numVisits) / numVisits);
        }
    }
}