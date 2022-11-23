namespace Chess
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using UnityEngine;
	using static System.Math;
	class MCTSSearch : ISearch
	{
		public event System.Action<Move> onSearchComplete;

		MoveGenerator moveGenerator;

		Move bestMove;
		int bestEval;
		bool abortSearch;

		MCTSSettings settings;
		Board board;
		Evaluation evaluation;

		// Diagnostics
		public SearchDiagnostics Diagnostics { get; set; }

		public MCTSSearch(Board board, MCTSSettings settings)
		{
			this.board = board;
			this.settings = settings;
			evaluation = new Evaluation();
			moveGenerator = new MoveGenerator();
		}

		public void StartSearch()
		{
			InitDebugInfo();

			// Initialize search settings
			bestEval = 0;
			bestMove = Move.InvalidMove;

			moveGenerator.promotionsToGenerate = settings.promotionsToSearch;
			abortSearch = false;
			Diagnostics = new SearchDiagnostics();

			SearchMoves();

			onSearchComplete?.Invoke(bestMove);

			if (!settings.useThreading)
			{
				LogDebugInfo();
			}
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
			if (abortSearch)
			{
				return;
			}

			// TODO
			// Don't forget to make sure to terminate the search once abortSearch is set to true.
		}

		void LogDebugInfo()
		{
			// Optional
		}

		void InitDebugInfo()
		{
			// Optional
		}
	}
}
