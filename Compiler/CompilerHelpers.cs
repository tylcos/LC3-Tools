using System;
using System.Collections.Generic;
using System.Linq;

namespace LC3.Compiler
{
    class CompilerHelpers
    {
		public static List<Lexeme> CreateConditional(List<Lexeme> conditional, List<Lexeme> block0)
		{
			var block = new List<Lexeme>(4);

			// if a comparison b
			Lexeme a = conditional[0];
			Lexeme b = conditional[2];
			Comparison c = new Comparison(conditional[1]);

			// 0 == a  ->  a == 0
			if (a == "0")
				(a, b) = (b, a);

			if (b != "0")
			{
				block.Add(new Lexeme($"NOT r5 {b}"));
				block.Add(new Lexeme($"ADD r5 r5 1"));
				block.Add(new Lexeme($"ADD r5 r5 {a}"));
			}
			else if (NeedsCCSet(block0, a))
				block.Add(new Lexeme($"ADD {a} {a} 0"));

			block.Add(new Lexeme($"BR{!c} ENDIF"));


			return block;
		}



		static bool NeedsCCSet(List<Lexeme> block, Lexeme reg)
		{
			if (block == null)
				return true;


			for (int i = block.Count - 1; i >= 0; i--)
			{
				var currInst = block[i];
				if (currInst.SetsCC)
					return currInst.R0 != reg;
			}


			return true;
		}
	}
}
