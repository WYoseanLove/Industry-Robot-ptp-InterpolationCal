using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tecnomatix.Engineering;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace TxCommand
{
    public class TxItemsDimensionalVec
      {
       public  TxItemsDimensionalVec() {

            ITxObject selectItem = TxApplication.ActiveSelection.GetLastPickedItem() as ITxObject;
            if (selectItem == null)
            {
                //TxMessageBox.Show("Please select the Weld OP firstly before connect!", "Warnning", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ITxStorable storable = selectItem as ITxStorable;
            if (storable == null)
            {
                return;
            }

            TxStorage storage = storable.StorageObject;
            TxLibraryStorage libraryStorage = storage as TxLibraryStorage;
            if (libraryStorage == null)
            {
                return;
            }

            string cojtPath = libraryStorage.FullPath;

            Action setModelingScope;
            Func<TxTypeFilter, TxObjectList> getAllDescendants;
            Action<string> endModeling;

            if (selectItem is TxComponent component)
            {
                setModelingScope = component.SetModelingScope;
                getAllDescendants = component.GetAllDescendants;
                endModeling = component.EndModeling;
            }
            else if (selectItem is TxServoGun servoGun)
            {
                setModelingScope = servoGun.SetModelingScope;
                getAllDescendants = servoGun.GetAllDescendants;
                endModeling = servoGun.EndModeling;
            }
            else if (selectItem is TxDevice device)
            {
                setModelingScope = device.SetModelingScope;
                getAllDescendants = device.GetAllDescendants;
                endModeling = device.EndModeling;
            }
            else if (selectItem is TxGripper gripper)
            {
                setModelingScope = gripper.SetModelingScope;
                getAllDescendants = gripper.GetAllDescendants;
                endModeling = gripper.EndModeling;
            }
            else
            {
                return;
            }

            setModelingScope();

            TxObjectList dimensionals = getAllDescendants(new TxTypeFilter(typeof(ITx2Or3DimensionalGeometry)));

            // 桌面路径 + data.csv
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string name = selectItem.Name;
            string csvPath = Path.Combine(desktopPath, $"{name}.csv");

            // 若文件不存在则创建（这里创建时顺便写个表头，如不需要可去掉）
            if (!File.Exists(csvPath))
            {
                using (StreamWriter sw = File.CreateText(csvPath))
                {
                    sw.WriteLine("X,Y,Z");
                }
            }

            if (dimensionals.Count > 0)
            {
                // 用 StreamWriter 以追加模式打开，逐行写入
                using (StreamWriter sw = new StreamWriter(csvPath, append: true))
                {
                    for (int i = 0; i < dimensionals.Count; i++)
                    {
                        TxApproximationData Approximation = (dimensionals[i] as ITx2Or3DimensionalGeometry).Approximation;
                        
                        Approximation.Points.ToList().ForEach(p =>
                        {
                            sw.WriteLine($"{p.X},{p.Y},{p.Z}");
                        });

                    }
                }
            }
          
            endModeling(cojtPath);

        }

    }
}
