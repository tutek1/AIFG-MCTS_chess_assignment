namespace Chess
{
    using System;
    using System.Collections.Generic;
    using System.Data.Odbc;
    using System.Threading;
    using UnityEngine;
    using static System.Math;
    public class MCTSSearch : ISearch
    {
        public event System.Action<Move> onSearchComplete;

        MoveGenerator moveGenerator;

        Move bestMove;
        int numSimulations = 0;
        bool abortSearch;

        MCTSSettings settings;
        Board board;
        Evaluation evaluation;

        MCTSNode rootNode;

        System.Random rand;

        // Diagnostics
        public SearchDiagnostics Diagnostics { get; set; }
        System.Diagnostics.Stopwatch searchStopwatch;

        // Struct for backpropagation
        public struct SimResult 
        {
            public float whiteValue;
            public float blackValue;
        }

        public MCTSSearch(Board board, MCTSSettings settings)
        {
            this.board = board;
            this.settings = settings;
            evaluation = new Evaluation();
            moveGenerator = new MoveGenerator();
            moveGenerator.promotionsToGenerate = settings.promotionsToSearch;

            rand = new System.Random();
        }

        public void StartSearch()
        {
            InitDebugInfo();

            // Initialize search settings
            abortSearch = false;
            Diagnostics = new SearchDiagnostics();
            numSimulations = 0;

            // Create root (when created in constructor it brokes everything)
            rootNode = new MCTSNode(null, board, Move.InvalidMove);
            rootNode.allPossibleMoves = moveGenerator.GenerateMoves(board, true);
            rootNode.lastExpandedMoveIdx = rootNode.allPossibleMoves.Count;

            // Default move
            bestMove = rootNode.allPossibleMoves[0];

            // If there are more possible moves than one do the search
            if (rootNode.allPossibleMoves.Count != 1) SearchMoves();

            // Invoke the search complete
            onSearchComplete?.Invoke(bestMove);

            EndDebugInfo();
        }

        public void EndSearch()
        {
            if (settings.useTimeLimit)
            {
                abortSearch = true;
            }
        }

        void SearchMoves()
        {
            while (!abortSearch && (!settings.limitNumOfPlayouts || numSimulations < settings.maxNumOfPlayouts))
            {
                // Selection step
                MCTSNode nodeToExpand = SelectNode(rootNode);
                if (abortSearch) break;

                // Expansion
                MCTSNode nodeToSimulate = ExpandNode(nodeToExpand);
                if (abortSearch) break;
                
                // Simulation
                SimResult simResult = Simulate(nodeToSimulate);
                if (abortSearch) break;

                // Backpropagation
                nodeToSimulate.Backpropagate(simResult);
                if (abortSearch) break;

                // Select the best move
                bestMove = rootNode.SelectBestMove();
            }
        }

        private MCTSNode SelectNode(MCTSNode node)
        {
            while (node.children.Count > 0 && !abortSearch)
            {
                MCTSNode bestChild = node.SelectBestChild();
                if (bestChild == node) break;
                node = bestChild;
            }

            return node;
        }


        private MCTSNode ExpandNode(MCTSNode nodeToExpand)
        {
            if (nodeToExpand.allPossibleMoves.Count != nodeToExpand.children.Count)
            {
                // Get the next move to expand
                nodeToExpand.lastExpandedMoveIdx--;
                Move move = nodeToExpand.allPossibleMoves[nodeToExpand.lastExpandedMoveIdx];

                // Apply the move to the board
                Board newState = nodeToExpand.state.Clone();
                newState.MakeMove(move);

                // Create a new child node
                MCTSNode childNode = new MCTSNode(
                    nodeToExpand,
                    newState,
                    move
                );

                // Generate possible moves for the new node
                childNode.allPossibleMoves = moveGenerator.GenerateMoves(newState, false);
                childNode.lastExpandedMoveIdx = childNode.allPossibleMoves.Count;

                // Add the child to the node's children
                nodeToExpand.children.Add(childNode);

                return childNode; // Expansion successful -> simulate the new node
            }

            return nodeToExpand; // No moves left to expand -> simulate this node
        }

        private SimResult Simulate(MCTSNode nodeToSimulate)
        {
            // Clone the board state using the lightweight clone method
            SimPiece[,] simState = nodeToSimulate.state.GetLightweightClone();

            // Set up the simulation variables
            int simulationDepth = 0;
            bool whiteMove = !nodeToSimulate.state.WhiteToMove;
            bool hasKingBeenCaptured = false;

            while (!abortSearch && simulationDepth < settings.playoutDepthLimit)
            {
                // Generate possible sim moves for the current state
                List<SimMove> possibleMoves = moveGenerator.GetSimMoves(simState, whiteMove);

                // Check if there are no possible moves (end of simulation)
                if (possibleMoves.Count == 0)
                {
                    break;
                }

                // Find out which move leads to the best value
                SimMove bestMove = possibleMoves[0];
                float bestValue = -1;
                foreach (SimMove move in possibleMoves)
                {
                    SimPiece[,] copy = simState.Clone() as SimPiece[,];
                    ApplySimMove(copy, move);

                    float value = IsKingCaptured(copy)? 1 : evaluation.EvaluateSimBoard(copy, whiteMove);
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestMove = move;
                    }
                }

                // Pseudo randomly select a move to play out (gives more likelyhood of selecting the highest value move)
                SimMove selectedMove = rand.Next(possibleMoves.Count/4) == 0? bestMove : possibleMoves[rand.Next(possibleMoves.Count)];

                // Apply the selected move to the simulation state
                ApplySimMove(simState, selectedMove);

                // Switch turns
                whiteMove = !whiteMove;

                // Check for king capture (end of game)
                if (IsKingCaptured(simState))
                {
                    hasKingBeenCaptured = true;
                    break;
                }

                simulationDepth++;
            }

            // Evaluate the resulting state
            SimResult result = new SimResult();
            if (hasKingBeenCaptured)
            {
                // Win for the current player if the opponent's king is captured
                if (whiteMove)
                {
                    result.whiteValue = 1;
                    result.blackValue = 0;
                }
                else
                {
                    result.whiteValue = 0;
                    result.blackValue = 1;
                }
            }
            else
            {
                // Evaluate the board for intermediate results
                result.whiteValue = evaluation.EvaluateSimBoard(simState, false); 
                result.blackValue = 1-result.whiteValue;
            }

            numSimulations += 1;

            return result;
        }

        // Method to apply a SimMove to a lightweight board clone
        void ApplySimMove(SimPiece[,] simState, SimMove move)
        {
            SimPiece piece = simState[move.startCoord1, move.startCoord2];
            simState[move.startCoord1, move.startCoord2] = null;
            simState[move.endCoord1, move.endCoord2] = piece;
        }

        // Method to check if a king has been captured in the simulation state
        bool IsKingCaptured(SimPiece[,] simState)
        {
            bool whiteKingExists = false;
            bool blackKingExists = false;

            for (int row = 0; row < simState.GetLength(0); row++)
            {
                for (int col = 0; col < simState.GetLength(1); col++)
                {
                    SimPiece piece = simState[row, col];
                    if (piece != null && piece.type == SimPieceType.King)
                    {
                        if (simState[row, col].team)
                        {
                            whiteKingExists = true;
                        }
                        else
                        {
                            blackKingExists = true;
                        }
                    }

                    if (whiteKingExists && blackKingExists)
                    {
                        return false; // Both kings are still on the board
                    }
                }
            }

            return !(whiteKingExists && blackKingExists); // True if one of the kings is missing
        }

        void EndDebugInfo()
        {
            Debug.Log("MTCS stopped search in " + searchStopwatch.Elapsed);
        }

        void InitDebugInfo()
        {
            searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log("MTCS start search");
        }
    }
}