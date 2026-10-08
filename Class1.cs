
using Microsoft.Office.Interop.Excel;
using Tecnomatix.Engineering;
using Try_teccommand;

namespace TxCommand
{
    public class TxCommand :TxButtonCommand
    {
        public override string Category
        {

            get
            {
                return "Application";
            }
        }

        public override string Name
        {

            get
            {
                return "TecCommand";
            }
        }

        public override void Execute(object cmdParams)
        {

            // TxRobotptpPathCal pathCal = new TxRobotptpPathCal();
            TxRobotptpInterpolationCal pathCalForKawasaki = new TxRobotptpInterpolationCal();
            //TxItemsDimensionalVec itemsDimensionalVec = new TxItemsDimensionalVec();

        }





    }
}
