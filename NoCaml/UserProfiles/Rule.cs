using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace NoCaml.UserProfiles
{
    public class Rule
    {
        public string Left { get; set; }
        public string Operator { get; set; }
        public string Right { get; set; }



        private static PropertyInfo piLeftContent;
        private static PropertyInfo piRightContent;
        private static PropertyInfo piOperator;
        private static Type TARC;

        public Rule() { }
        public Rule(string left, string op, string right) {
            Left = left;
            Operator = op;
            Right = right;
        }

        public Rule(object arc)
        {
            if (TARC == null)
            {
                TARC = arc.GetType();
                piLeftContent = TARC.GetProperty("LeftContent");
                piRightContent = TARC.GetProperty("RightContent");
                piOperator = TARC.GetProperty("Operator");
            }

            Left = piLeftContent.GetValue(arc, new object[] { }) as string;
            Right = piRightContent.GetValue(arc, new object[] { }) as string;
            Operator = piOperator.GetValue(arc, new object[] { }) as string;

        }
    }
}