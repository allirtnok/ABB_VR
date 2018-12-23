using ABB.Robotics.RobotStudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VrPaintAddin
{
    public static class HelperFunctions
    {
        public static void WithUndo(string name, Action operation)
        {
            Project.UndoContext.BeginUndoStep(name);
            try
            {
                operation();
            }
            catch
            {
                Project.UndoContext.CancelUndoStep(CancelUndoStepType.Rollback);
            }
            finally
            {
                Project.UndoContext.EndUndoStep();
            }
        }

        public static void WithUndoAppend(string name, Action operation)
        {
            Project.UndoContext.AppendToUndoStep(name);
            try
            {
                operation();
            }
            catch
            {
                Project.UndoContext.CancelUndoStep(CancelUndoStepType.Rollback);
            }
            finally
            {
                Project.UndoContext.EndUndoStep();
            }
        }
    }
}
