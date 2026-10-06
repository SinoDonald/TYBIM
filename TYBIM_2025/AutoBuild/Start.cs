using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;

namespace TYBIM_2025.AutoBuild
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class Start : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return OpenForm(commandData, "柱");
        }

        internal static Result OpenForm(ExternalCommandData commandData, string elementType)
        {
            if (commandData.Application.ActiveUIDocument == null) return Result.Cancelled;
            // 共用 CAD 資料，因此切換指令時關閉舊視窗，再讀取目前文件。
            Form myForm = Application.OpenForms["LayersForm"];
            if (myForm is LayersForm pendingForm && pendingForm.HasPendingEvent)
            {
                myForm.BringToFront();
                return Result.Succeeded;
            }
            if (myForm != null) myForm.Close();
            LayersForm layersForm = new LayersForm(commandData.Application.ActiveUIDocument, elementType);
            layersForm.Show();

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class StartWalls : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Start.OpenForm(commandData, "牆");
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class StartBeams : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Start.OpenForm(commandData, "樑");
        }
    }
}
