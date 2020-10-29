
using System.Collections.Generic;

namespace LC3.Compiler
{
    class Lexeme
    {
        public string Value { get; init; }
        public readonly string[] Split;


        public Lexeme(string value)
        {
            Value = value;
            Split = Value.Split(" ");
        }




        private static List<string> setCCInstructions = new() { "and", "add", "not", "ld", "ldi", "ldr" };
        public bool SetsCC => setCCInstructions.Contains(Split[0].ToLower());


        public string R0 => Split[1];




        public static implicit operator string(Lexeme a) => a.Value;


        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;
    }
}