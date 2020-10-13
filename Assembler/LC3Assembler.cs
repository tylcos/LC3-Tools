using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LC3
{
    class LC3Assembler
    {
        public List<Instruction>               Instructions { get; private set; } = new();
        public List<(int line, string msg)>    Errors       { get; private set; } = new();
        public Dictionary<string, int>         Labels       { get; private set; } = new();

        private record LabelRefrence(int Line, int OffsetSize, string Label);
        private List<LabelRefrence> LabelReferences;

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
            ["halt"]     = (1, 0xF000),
            [".fill"]    = (2, 0x0000),
            [".blkw"]    = (2, 0x0000),
            [".stringz"] = (2, 0x0000),
            [".orig"]    = (2, 0x0000),
            [".end"]     = (1, 0x0000)
        };


        public List<Instruction> Assemble(string path) => Assemble(File.ReadLines(path));



        /// <summary>
        ///     Assembles the provided program without stopping for errors.
        ///    
        ///     First  pass: Assembles all instructions and pseudo-ops. Creates symbol table in 'Labels' and adds any unrecognized labels to 'LabelReferences'.
        ///     Second pass: Each reference in 'LabelReferences' is dereferenced by querying the symbol table.
        /// </summary>
        /// <param name="lines">Provided program</param>
        /// <returns>Assembled program</returns>
        public List<Instruction> Assemble(IEnumerable<string> lines)
        {
            Instructions    = new(32);
            Errors          = new();
            Labels          = new();
            LabelReferences = new();


            string[] parts = null;                                     // Current line split on delimiter
            string opcode = "";                                        // Current opcode
            (int argumentCount, int opcode) instructionInfo = default; // Info about current opcode


            lineNum = -1;
            foreach (string line in lines)
            {
                lineNum++;

                if (!ParseLine(line))
                    continue;


                int CurrentInstruction = 0;
                switch (opcode)
                {
                    case "add":  // ADD r0 r0 r0
                    case "and":  // AND r0 r0 0
                        CurrentInstruction = parts[3][0] == 'r'
                            ? instructionInfo.opcode + Register(1, 9) + Register(2, 6) + Register(3, 0)
                            : instructionInfo.opcode + Register(1, 9) + Register(2, 6) + 0x0020 + Offset(3, 5);
                        break;
                    case "not":  // NOT r0 r0
                        CurrentInstruction = instructionInfo.opcode + Register(1, 9) + Register(2, 6) + 0x003F;
                        break;


                    case "br":
                        int conditions = 0x0E00;
                        if (!parts[1].Contains("n")) conditions &= 0xF7FF;
                        if (!parts[1].Contains("z")) conditions &= 0xFBFF;
                        if (!parts[1].Contains("p")) conditions &= 0xFDFF;

                        CurrentInstruction = instructionInfo.opcode + conditions + Offset(2, 9);
                        break;
                    case "jmp":  // JMP r0
                    case "jsrr": // JSRR r0
                        CurrentInstruction = instructionInfo.opcode + Register(1, 6);
                        break;
                    case "ret":  // RET
                        CurrentInstruction = instructionInfo.opcode + 0x01C0;
                        break;
                    case "rti":  // RTI
                        CurrentInstruction = instructionInfo.opcode;
                        break;
                    case "jsr":  // JSR 0
                        CurrentInstruction = instructionInfo.opcode + 0x0800 + Offset(1, 11);
                        break;
                    case "trap": // TRAP 0
                        CurrentInstruction = instructionInfo.opcode + Offset(1, 8);
                        break;


                    case "ld":   // LD r0 0
                    case "ldi":  // LDI r0 0
                    case "lda":  // LDA r0 0
                    case "lea":  // LEA r0 0
                    case "st":   // ST r0 0
                    case "sti":  // STI r0 0
                        CurrentInstruction = instructionInfo.opcode + Register(1, 9) + Offset(2, 9);
                        break;
                    case "ldr":  // LDR r0 r0 0
                    case "str":  // STR r0 r0 0
                        CurrentInstruction = instructionInfo.opcode + Register(1, 9) + Register(2, 6) + Offset(3, 6);
                        break;


                    case "halt":      // HALT
                        CurrentInstruction = instructionInfo.opcode + 0x0025;
                        break;
                    case ".fill":     // .FILL 0
                        CurrentInstruction = Offset(1, 16);
                        break;
                    case ".blkw":     // .BLKW 1
                        int length = Offset(1, 16, false);

                        for (int i = 0; i < length; i++)
                            Instructions.Add(Instruction.Empty);

                        continue;
                    case ".stringz":  // .STRINGZ "Hi" -> .stringz Hi
                        foreach (char c in parts[1])
                            Instructions.Add(new Instruction(Convert.ToInt32(c)));

                        Instructions.Add(Instruction.Empty);
                        continue;
                    case ".orig":    // .ORIG x3000
                    case ".end":     // .END
                        continue;
                }


                Instructions.Add(new Instruction(CurrentInstruction));
            }


            AssignLabelRefrences();

            return Instructions;


            int currentPC() => Instructions.Count;

            int Register(int partNum, int offset = 0)
            {
                string regString = parts[partNum];
                if (SyntaxErrorIf(regString.Length != 2, $"Invalid register '{regString}', must be 2 characters."))
                    return 0;

                int reg = regString[1] - '0';
                if (SyntaxErrorIf(reg < 0 || reg > 7, $"Invalid register: '{regString}', must be in range r0 - r7."))
                    return 0;

                return reg << offset;
            }

            int Offset(int partNum, int offsetSize, bool allowLabels = true)
            {
                string offsetString = parts[partNum];
                string label = "";


                int offset = 0;
                // Hex
                if (offsetString[0] == 'x')
                {
                    bool validOffset = int.TryParse(offsetString[1..], NumberStyles.HexNumber, null, out offset);
                    SyntaxErrorIf(!validOffset, "Cannot parse hex offset: " + offsetString);
                }
                // Decimal
                else if (char.IsDigit(offsetString[0]) || offsetString[0] == '-') 
                {
                    bool validOffset = int.TryParse(parts[partNum], out offset);
                    SyntaxErrorIf(!validOffset, "Cannot parse offset: " + offsetString);
                }
                // Known label 
                else if (allowLabels && Labels.TryGetValue(offsetString, out int labelPosition))
                {
                    offset = labelPosition - currentPC() - 1;
                    label = offsetString;
                }
                // Unknown label
                else if (allowLabels && IsValidLabel(offsetString))
                {
                    LabelReferences.Add(new LabelRefrence(currentPC(), offsetSize, offsetString));
                    return 0;
                }


                return IsValidOffset(offset, offsetSize, label) ? offset & ((1 << offsetSize) - 1) : 0;
            }

            // Overwrites parts, opcode, instructionInfo
            bool ParseLine(string line)
            {
                // Cannot trim line if it is a string instruction
                if (line.Length > 10 && line[..8] == ".STRINGZ")
                {
                    parts = new []{ ".stringz", line[8..]};

                    Range range = (parts[1].IndexOf("\"") + 1) .. parts[1].LastIndexOf("\"");
                    parts[1] = parts[1][range];
                    return true;
                }
                    


                // Could make this far more efficient
                string trimmedLine = line.Trim().ToLower().Split(new[] { ';' }, 2)[0];
                if (trimmedLine.Length == 0)
                    return false;
                parts = trimmedLine.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                opcode = parts[0];


                // Deal with BR instruction, BRnzp 0 -> BR nzp 0
                if (opcode.Length >= 2 && opcode[..2] == "br")
                {
                    parts = opcode.Length == 2
                        ? new string[] { "br", "nzp", parts[1] }
                        : new string[] { "br", opcode[2..], parts[1] };
                    opcode = "br";
                }


                // Check for label before instruction
                if (IsValidLabel(opcode))
                {
                    Labels.Add(opcode, currentPC());

                    if (parts.Length > 1) // LABEL BR 0
                    {
                        parts = parts[1..];
                        opcode = parts[0];
                    }
                    else                  // LABEL
                        return false;
                }


                // Check for valid instruction
                if (SyntaxErrorIf(!InstructionInfo.TryGetValue(opcode, out instructionInfo), 
                        $"Instruction '{opcode}' not recognized.")
                    || SyntaxErrorIf(parts.Length != instructionInfo.argumentCount, 
                        $"Invalid {opcode.ToUpper()} instruction, needs {instructionInfo.argumentCount} arguments."))
                    return false;

                return true;
            }
        }


        private void AssignLabelRefrences()
        {
            foreach ((int line, int offsetSize, string label) in LabelReferences)
            {
                if (SyntaxErrorIf(!Labels.TryGetValue(label, out int labelPosition), $"Label '{label}' cannot be found."))
                    continue;

                int offset = labelPosition - line - 1;
                int sizeMask = (1 << offsetSize) - 1;

                if (!IsValidOffset(offset, offsetSize, label))
                    continue;

                Instructions[line] = new Instruction(Instructions[line].Bits | (offset & sizeMask));
            }
        }




        /// <summary>
        ///     Adds error and returns True if 'check' is True.
        /// </summary>
        /// <param name="check">True for bad syntax. </param>
        /// <param name="errorMsg">Error message to be added if assertion is False. </param>
        /// <returns>True if error was added</returns>
        private bool SyntaxErrorIf(bool check, string errorMsg)
        {
            if (check)
                Errors.Add((lineNum, errorMsg));

            return check;
        }


        private bool IsValidLabel(string label)
        { 
            if (label.Length == 0 || InstructionInfo.ContainsKey(label.ToLower())
                || SyntaxErrorIf(Labels.TryGetValue(label, out int labelPos), $"Label '{label}' already declared on line {labelPos}.") 
                || SyntaxErrorIf(label[0] == 'x' || char.IsDigit(label[0]),   $"Label '{label}' cannot start with 'x' or a digit."))
                return false;

            return true;
        }

        private bool IsValidOffset(int offset, int offsetSize, string label = "")
        {
            int sizeMask = 1 << (offsetSize - 1);
            bool valid = offset < 0 ? -offset <= sizeMask : offset < sizeMask;


            int limit = offset < 0 ? -sizeMask : sizeMask - 1;
            string errorMsg = label == ""
                ? $"Offset '{offset}' cannot fit within {offsetSize} bits, the limit is {limit}."
                : $"Offset for label '{label}' with value {offset} cannot fit within {offsetSize} bits, the limit is {limit}.";
            SyntaxErrorIf(!valid, errorMsg);


            return valid;
        }
    }
}
