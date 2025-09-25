using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Forms;
using Form = System.Windows.Forms.Form;

namespace TYBIM_2025
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class Start : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // 檢查該 Form 是否已經開啟
            Form myForm = Application.OpenForms["LayersForm"];

            if (myForm == null)  // 尚未開啟
            {
                LayersForm layersForm = new LayersForm(commandData.Application.ActiveUIDocument);
                layersForm.Show();
            }
            else // 已經開啟 -> 讓它跳到最前面
            {
                myForm.BringToFront();
                myForm.WindowState = FormWindowState.Normal;
            }

            return Result.Succeeded;
        }
    }
}
