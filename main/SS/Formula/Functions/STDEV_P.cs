using NPOI.SS.Formula.Eval;
using System;
using System.Collections.Generic;
using System.Text;
using static NPOI.SS.Formula.Functions.AggregateFunction;

namespace NPOI.SS.Formula.Functions
{
    public class STDEV_P : FreeRefFunction
    {
        public static FreeRefFunction Instance = new STDEV_P();

        public ValueEval Evaluate(ValueEval[] args, OperationEvaluationContext ec)
        {
            var values = ValueCollector.CollectValues(args);

            if (values.Length < 1)
            {
                throw new EvaluationException(ErrorEval.DIV_ZERO);
            }

            return new NumberEval(StatsLib.stdevp(values));
        }
    }
}
