using UnityEngine;
using System;
namespace TMPro
{
    /// <summary>
    /// EXample of a Custom Character Input Validator to only allow digits from 0 to 9.
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "Username Validator", menuName = "TextMeshPro/Input Validators/Username", order = 100)]
    public class Validator : TMP_InputValidator
    {
        // Custom text input validation function
        public override char Validate(ref string text, ref int pos, char ch)
        {
            //disallow dangerous characters that could fuck up a save file
            if(ch == '*' || ch == '\b' || ch == '\n' || ch == '\r' || ch == '\\' || ch == ';' || ch == '\v')
            {
                return (char)0;
            }

            text += ch;
            pos += 1;
            return ch;
        }
    }
}