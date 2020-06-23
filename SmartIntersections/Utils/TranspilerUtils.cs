using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SmartIntersections.Patches {
    using System.Reflection;
    using System.Linq;
    using System.Diagnostics;

    public static class TranspilerUtils {
        static bool VERBOSE => false;

        static void Log(object message) {
            UnityEngine.Debug.Log("TRANSPILER " + message);
        }

        /// <typeparam name="TDelegate">delegate type</typeparam>
        /// <returns>Type[] represeting arguments of the delegate.</returns>
        internal static Type[] GetParameterTypes<TDelegate>() where TDelegate : Delegate {
            return typeof(TDelegate).GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
        }

        /// <summary>
        /// Gets directly declared method.
        /// </summary>
        /// <typeparam name="TDelegate">delegate that has the same argument types as the intented overloaded method</typeparam>
        /// <param name="type">the class/type where the method is delcared</param>
        /// <param name="name">the name of the method</param>
        /// <returns>a method or null when type is null or when a method is not found</returns>
        internal static MethodInfo DeclaredMethod<TDelegate>(Type type, string name)
            where TDelegate : Delegate {
            var args = GetParameterTypes<TDelegate>();
            var ret = AccessTools.DeclaredMethod(type, name, args);
            if (ret == null)
                Log($"failed to retrieve method {type}.{name}({args})");
            return ret;
        }

        public static List<CodeInstruction> ToCodeList(IEnumerable<CodeInstruction> instructions) {
            var originalCodes = new List<CodeInstruction>(instructions);
            var codes = new List<CodeInstruction>(originalCodes);
            return codes;
        }

        public static CodeInstruction GetLDArg(MethodInfo method, string argName) {
            int idx = GetParameterLoc(method, argName);
            if (idx == -1) return null;
            if (!method.IsStatic)
                idx++; // first argument is object instance.
            if (idx == 0) {
                return new CodeInstruction(OpCodes.Ldarg_0);
            } else if (idx == 1) {
                return new CodeInstruction(OpCodes.Ldarg_1);
            } else if (idx == 2) {
                return new CodeInstruction(OpCodes.Ldarg_2);
            } else if (idx == 3) {
                return new CodeInstruction(OpCodes.Ldarg_3);
            } else {
                return new CodeInstruction(OpCodes.Ldarg_S, idx);
            }
        }

        /// <summary>
        /// Post condtion: for instnace method add one to get argument location
        /// </summary>
        /// <return>Parameter index (excluding instance) if sucessful
        /// -1 otherwise.</return>
        public static int GetParameterLoc(MethodInfo method, string name) {
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; ++i) {
                if (parameters[i].Name == name) {
                    return i;
                }
            }
            return -1;
            //throw new Exception($"did not found parameter with name:<{name}>");
        }

        public static bool IsSameInstruction(CodeInstruction a, CodeInstruction b, bool debug = false) {
            if (a.opcode == b.opcode) {
                if (a.operand == b.operand) {
                    return true;
                }

                // This special code is needed for some reason because the == operator doesn't work on System.Byte
                return (a.operand is byte aByte && b.operand is byte bByte && aByte == bByte);
            } else {
                return false;
            }
        }

        public static bool IsLdLoc(CodeInstruction instruction) {
            return (instruction.opcode == OpCodes.Ldloc_0 || instruction.opcode == OpCodes.Ldloc_1 ||
                    instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Ldloc_3
                    || instruction.opcode == OpCodes.Ldloc_S || instruction.opcode == OpCodes.Ldloc
                );
        }

        public static bool IsStLoc(CodeInstruction instruction) {
            return (instruction.opcode == OpCodes.Stloc_0 || instruction.opcode == OpCodes.Stloc_1 ||
                    instruction.opcode == OpCodes.Stloc_2 || instruction.opcode == OpCodes.Stloc_3
                    || instruction.opcode == OpCodes.Stloc_S || instruction.opcode == OpCodes.Stloc
                );
        }

        /// <summary>
        /// Get the instruction to load the variable which is stored here.
        /// </summary>
        public static CodeInstruction BuildLdLocFromStLoc(CodeInstruction instruction) {
            if (instruction.opcode == OpCodes.Stloc_0) {
                return new CodeInstruction(OpCodes.Ldloc_0);
            } else if (instruction.opcode == OpCodes.Stloc_1) {
                return new CodeInstruction(OpCodes.Ldloc_1);
            } else if (instruction.opcode == OpCodes.Stloc_2) {
                return new CodeInstruction(OpCodes.Ldloc_2);
            } else if (instruction.opcode == OpCodes.Stloc_3) {
                return new CodeInstruction(OpCodes.Ldloc_3);
            } else if (instruction.opcode == OpCodes.Stloc_S) {
                return new CodeInstruction(OpCodes.Ldloc_S, instruction.operand);
            } else if (instruction.opcode == OpCodes.Stloc) {
                return new CodeInstruction(OpCodes.Ldloc, instruction.operand);
            } else {
                throw new Exception("Statement is not stloc!");
            }
        }

        public static CodeInstruction BuildLdLocFromLdLoc(CodeInstruction instruction) {
            if (instruction.opcode == OpCodes.Ldloc_0) {
                return new CodeInstruction(OpCodes.Ldloc_0);
            } else if (instruction.opcode == OpCodes.Ldloc_1) {
                return new CodeInstruction(OpCodes.Ldloc_1);
            } else if (instruction.opcode == OpCodes.Ldloc_2) {
                return new CodeInstruction(OpCodes.Ldloc_2);
            } else if (instruction.opcode == OpCodes.Ldloc_3) {
                return new CodeInstruction(OpCodes.Ldloc_3);
            } else if (instruction.opcode == OpCodes.Ldloc_S) {
                return new CodeInstruction(OpCodes.Ldloc_S, instruction.operand);
            } else if (instruction.opcode == OpCodes.Ldloc) {
                return new CodeInstruction(OpCodes.Ldloc, instruction.operand);
            } else {
                throw new Exception("Statement is not ldloc!");
            }
        }

        public static CodeInstruction BuildStLocFromLdLoc(CodeInstruction instruction) {
            if (instruction.opcode == OpCodes.Ldloc_0) {
                return new CodeInstruction(OpCodes.Stloc_0);
            } else if (instruction.opcode == OpCodes.Ldloc_1) {
                return new CodeInstruction(OpCodes.Stloc_1);
            } else if (instruction.opcode == OpCodes.Ldloc_2) {
                return new CodeInstruction(OpCodes.Stloc_2);
            } else if (instruction.opcode == OpCodes.Ldloc_3) {
                return new CodeInstruction(OpCodes.Stloc_3);
            } else if (instruction.opcode == OpCodes.Ldloc_S) {
                return new CodeInstruction(OpCodes.Stloc_S, instruction.operand);
            } else if (instruction.opcode == OpCodes.Ldloc) {
                return new CodeInstruction(OpCodes.Stloc, instruction.operand);
            } else {
                throw new Exception("Statement is not ldloc!");
            }
        }

        internal static string IL2STR(this IEnumerable<CodeInstruction> instructions) {
            string ret = "";
            foreach (var code in instructions) {
                ret += code + "\n";
            }
            return ret;
        }

        public class InstructionNotFoundException : Exception {
            public InstructionNotFoundException() : base() { }
            public InstructionNotFoundException(string m) : base(m) { }

        }

        public static int SearchInstruction(List<CodeInstruction> codes, CodeInstruction instruction, int index, int dir = +1, int counter = 1) {
            try {
                return SearchGeneric(codes, idx => IsSameInstruction(codes[idx], instruction), index, dir, counter);
            }
            catch (InstructionNotFoundException) {
                throw new InstructionNotFoundException(" Did not found instruction: " + instruction);
            }
        }

        public delegate bool Comperator(int idx);
        public static int SearchGeneric(List<CodeInstruction> codes, Comperator comperator, int index, int dir = +1, int counter = 1) {
            int count = 0;
            for (; 0 <= index && index < codes.Count; index += dir) {
                if (comperator(index)) {
                    if (++count == counter)
                        break;
                }
            }
            if (count != counter) {
                throw new InstructionNotFoundException(" Did not found instruction[s]. Comperator =  " + comperator);
            }
            Log("Found : \n" + new[] { codes[index], codes[index + 1] }.IL2STR());
            return index;
        }

        public static Label GetContinueLabel(List<CodeInstruction> codes, int index, int counter = 1, int dir = -1) {
            // continue command is in form of branch into the end of for loop.
            index = SearchGeneric(codes, idx => IsBR32(codes[idx].opcode), index, dir: dir, counter: counter);
            return (Label)codes[index].operand;
        }

        public static bool IsBR32(OpCode opcode) {
            // TODO complete list.
            return opcode == OpCodes.Br || opcode == OpCodes.Brtrue || opcode == OpCodes.Brfalse || opcode == OpCodes.Beq;
        }

        public static void MoveLabels(CodeInstruction source, CodeInstruction target) {
            // move labels
            var labels = source.labels;
            target.labels.AddRange((IEnumerable<Label>)labels);
            labels.Clear();
        }

        /// <summary>
        /// replaces one instruction at the given index with multiple instrutions
        /// </summary>
        public static void ReplaceInstructions(List<CodeInstruction> codes, CodeInstruction[] insertion, int index) {
            foreach (var code in insertion)
                if (code == null)
                    throw new Exception("Bad Instructions:\n" + insertion.IL2STR());
            if(VERBOSE)
                Log($"replacing <{codes[index]}>\nInsert between: <{codes[index - 1]}>  and  <{codes[index + 1]}>");

            MoveLabels(codes[index], insertion[0]);
            codes.RemoveAt(index);
            codes.InsertRange(index, insertion);

            if (VERBOSE)
                Log("Replacing with\n" + insertion.IL2STR());
            if (VERBOSE)
                Log("PEEK (RESULTING CODE):\n" + codes.GetRange(index - 4, insertion.Length + 8).IL2STR());
        }

        public static void InsertInstructions(List<CodeInstruction> codes, CodeInstruction[] insertion, int index, bool moveLabels=true) {
            foreach (var code in insertion)
                if (code == null)
                    throw new Exception("Bad Instructions:\n" + insertion.IL2STR());
            if (VERBOSE)
                Log($"Insert point:\n between: <{codes[index - 1]}>  and  <{codes[index]}>");

            MoveLabels(codes[index], insertion[0]);
            codes.InsertRange(index, insertion);

            if (VERBOSE)
                Log("\n" + insertion.IL2STR());
            if (VERBOSE)
                Log("PEEK:\n" + codes.GetRange(index - 4, insertion.Length+12).IL2STR());
        }
    }
}
