using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LC3
{
    class LC3Assembler
    {
        public List<(int, string)> Errors { get; } = new List<(int line, string msg)>();
        public Dictionary<string, int> Labels { get; } = new();

        private int lineNum = -1;


        public LC3Assembler()
        {
        }
            

        private static readonly Dictionary<string, (int argumentCount, int opcode)> InstructionInfo = new()
        {
            ["add"]  = (4, 0x1000),
            ["and"]  = (4, 0x5000),
            ["br"]   = (3, 0x0000),
            ["jmp"]  = (2, 0xC000),
            ["jsr"]  = (2, 0x4000),
            ["jsrr"] = (2, 0x4000),
            ["ld"]   = (3, 0x2000),
            ["ldi"]  = (3, 0xA000),
            ["ldr"]  = (4, 0x6000),
            ["lea"]  = (3, 0xE000),
            ["not"]  = (3, 0x9000),
            ["ret"]  = (1, 0xC000),
            ["rti"]  = (1, 0x8000),
            ["st"]   = (3, 0x3000),
            ["sti"]  = (3, 0xB000),
            ["str"]  = (4, 0x7000),
            ["trap"] = (2, 0xF000),

            // Pseudo-Ops
            ["halt"] = (1, 0xF000)
        };


        public List<Instruction> Assemble(string path) => Assemble(File.ReadLines(path));


        public List<Instruction> Assemble(IEnumerable<string> lines)
        {
            var instructions = new List<Instruction>(32);
            lineNum = -1;


            
            foreach (string line in lines)
            {
                lineNum++;


                string[] parts = line.ToLower().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;


                string opcode = parts[0];
                if (BadSyntaxCheck(!InstructionInfo.TryGetValue(opcode, out var info)
                        , $"Instruction '{opcode}' not recognized.") ||
                    BadSyntaxCheck(parts.Length != info.argumentCount
                        , $"Invalid {opcode.ToUpper()} instruciton, needs {info.argumentCount} arguments."))
                    continue;


                // Fix for invalid BR syntax used in class, BRnzp 0 -> BR nzp 0
                // and unconditional BR shortcut, BR 0 -> BR nzp 0
                if (opcode.Substring(0, 2) == "br" && parts.Length == 2)
                {
                    parts = opcode.Length == 2
                        ? new string[] { "br", "nzp", parts[1] }
                        : new string[] { "br", opcode.Substring(2), parts[1] };
                    opcode = "br";
                }


                int CurrentInstruction = 0;
                switch (opcode)
                {
                    case "add":  // ADD r0 r0 r0
                    case "and":  // AND r0 r0 0
                        CurrentInstruction = parts[3][0] == 'r'
                            ? info.opcode + Register(1, 9) + Register(2, 6) + Register(3, 0)
                            : info.opcode + Register(1, 9) + Register(2, 6) + 0x0020 + Offset(3, 5);
                        break;
                    case "not":  // NOT r0 r0
                        CurrentInstruction = info.opcode + Register(1, 9) + Register(2, 6) + 0x003F;
                        break;



                    case "br":   // BR nzp 0
                        int conditions = (parts[1].Contains("n") ? 0x0800 : 0) +
                                         (parts[1].Contains("z") ? 0x0400 : 0) +
                                         (parts[1].Contains("p") ? 0x0200 : 0);
                        CurrentInstruction = info.opcode + conditions + Offset(2, 9);
                        break;
                    case "jmp":  // JMP r0
                    case "jsrr": // JSRR r0
                        CurrentInstruction = info.opcode + Register(1, 6);
                        break;
                    case "ret":  // RET
                        CurrentInstruction = info.opcode + 0x01C0;
                        break;
                    case "rti":  // RTI
                        CurrentInstruction = info.opcode;
                        break;
                    case "jsr":  // JSR 0
                        CurrentInstruction = info.opcode + 0x0800 + Offset(1, 11);
                        break;
                    case "trap": // TRAP 0
                        CurrentInstruction = info.opcode + Offset(1, 8);
                        break;


                    case "ld":   // LD r0 0
                    case "ldi":  // LDI r0 0
                    case "lda":  // LDA r0 0
                    case "st":   // ST r0 0
                    case "sti":  // STI r0 0
                        CurrentInstruction = info.opcode + Register(1, 9) + Offset(2, 9);
                        break;
                    case "ldr":  // LDR r0 r0 0
                    case "str":  // STR r0 r0 0
                        CurrentInstruction = info.opcode + Register(1, 9) + Register(2, 6) + Offset(3, 6);
                        break;


                    case "halt":  // HALT
                        CurrentInstruction = info.opcode + 0x0025;
                        break;
                }


                instructions.Add(new Instruction(CurrentInstruction));
            }


            return instructions;


            int Register(int partNum, int offset = 0)
            {
                int reg = parts[partNum][1] - '0';

                if (BadSyntaxCheck(reg < 0 && reg > 7, "Invalid register number: " + parts[partNum]))
                    return 0;

                return reg << offset;
            }

            int Offset(int partNum, int length)
            {
                int lengthMask = (1 << length) - 1;

                string offsetString = parts[partNum];
                int offset = 0;


                if (offsetString[0] == 'x') // Hex prefix must be removed
                    BadSyntaxCheck(!int.TryParse(offsetString.Substring(1), NumberStyles.HexNumber, null, out offset), 
                        "Cannot parse offset: " + offsetString);
                else
                    BadSyntaxCheck(!int.TryParse(parts[partNum], out offset), "Cannot parse offset: " + offsetString);


                //int validTest = offset < 0 ? ~offset & (lengthMask >> 1) : offset & (lengthMask >> 1);
                bool withinRange = offset < 0 ? -offset <= (1 << (length - 1)) : offset <= (lengthMask >> 1);
                if (BadSyntaxCheck(!withinRange, $"Offset '{offset}' to large to fit in {length} bits."))
                    return 0;

                return offset & lengthMask;
            }
        }


        /// <summary>
        ///     Adds error and returns True if 'check' is True.
        /// </summary>
        /// <param name="check">True for bad syntax. </param>
        /// <param name="errorMsg">Error message to be added if assertion is False. </param>
        /// <returns>True if error was added</returns>
        private bool BadSyntaxCheck(bool check, string errorMsg)
        {
            if (check)
                Errors.Add((lineNum, errorMsg));

            return check;
        }
    }
}
