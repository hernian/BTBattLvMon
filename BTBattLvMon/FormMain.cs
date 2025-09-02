using BtMonEx02;
using System.Reflection;
using System.Windows.Forms;

namespace BTBattLvMon
{
    public partial class FormMain : Form
    {
        private BTConnBattMonitor _battMon = new();
        private int _guardCount = 0;

        public FormMain()
        {
            InitializeComponent();

            listViewInfo.Location = new Point(0, 0);
            listViewInfo.HeaderStyle = ColumnHeaderStyle.None;
            EnableControlDoubleBuffering(listViewInfo);
            AdjstSize();
        }

        private void EnableControlDoubleBuffering(Control control)
        {
            var prop = control.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(control, true, null);
        }

        private void AdjstSize()
        {
            if (listViewInfo.Items.Count == 0)
            {
                var lviNew = new ListViewItem();
                lviNew.SubItems.Add(string.Empty);
                listViewInfo.Items.Add(lviNew);
            }
            var lvi = listViewInfo.Items[0];
            var lineHeight = lvi.Bounds.Height;
            var height = lineHeight * listViewInfo.Items.Count;
            listViewInfo.Height = height + 1;
            this.Size = listViewInfo.Size;
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            timerUpdate.Start();
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            timerUpdate.Stop();
        }

        private async void TimerUpdate_Tick(object sender, EventArgs e)
        {
            if (_guardCount > 0)
            {
                return;
            }
            try
            {
                _guardCount++;
                var arStatus = await _battMon.GetStatusesAsync();
                while (listViewInfo.Items.Count < arStatus.Length)
                {
                    var lvi = new ListViewItem();
                    lvi.SubItems.Add(string.Empty);
                    listViewInfo.Items.Add(lvi);
                }
                for (int i = 0; i < arStatus.Length; i++)
                {
                    if (listViewInfo.Items.Count <= i)
                    {
                        var lviNew = new ListViewItem();
                        lviNew.SubItems.Add(string.Empty);
                        listViewInfo.Items.Add(lviNew);
                    }
                    var lvi = listViewInfo.Items[i];
                    var status = arStatus[i];
                    lvi.SubItems[0].Text = status.FriendlyName;
                    if (status.IsConnected)
                    {
                        lvi.SubItems[1].Text = $"{status.BatteryLevel}%";
                    }
                    else
                    {
                        lvi.SubItems[1].Text = "Ú‘±‚È‚µ";
                    }
                }
                while (listViewInfo.Items.Count > arStatus.Length)
                {
                    listViewInfo.Items[listViewInfo.Items.Count - 1].Remove();
                }
                AdjstSize();
            }
            catch { }
            finally
            {
                _guardCount--;
            }
        }
    }
}
