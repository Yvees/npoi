using NPOI.SS.Formula.Eval;
using System;
using System.Collections.Generic;
using System.Text;

namespace NPOI.SS.Formula.Functions
{
    public class STDEV_P : FreeRefFunction
    {
        public static FreeRefFunction Instance = new STDEV_P();

        public ValueEval Evaluate(ValueEval[] args, OperationEvaluationContext ec)
        {
            return args[0];
        }
    }
}
