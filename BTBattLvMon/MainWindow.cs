using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace BTBattLvMon
{
    internal class MainWindow : LayeredWindow
    {
        private const float FONT_SIZE_IN_POINT = 9f;
        private const float LINE_HEIGHT_RATIO = 1.2f;
        private const float NAME_WIDTH_RATIO = 10f;
        private const float BATTERY_LEVEL_WIDTH_RATIO = 5f;

        private readonly DrawingContext _drawingContext = new();
        private readonly BTConnBattMonitor _monitor = new();
        private IReadOnlyCollection<BattStatus> _statuses = [];
        private int _screenDpi = 96;
        private bool _isAdjustingLocation = false;
        private readonly System.Windows.Forms.Timer _locationAdjustTimer;

        public MainWindow()
        {
            this.Icon = Properties.Resources.Icon1;
            this.Visible = false;
            this.StartPosition = FormStartPosition.Manual;

            // BTバッテリ状態の監視開始
            _monitor.StartMonitor(this.OnBatteryStatusChanged);

            this.FormClosing += MainWindow_FormClosing;
            // ディスプレイ構成変更イベントの購読
            SystemEvents.DisplaySettingsChanged += MainWindow_DisplaySettingsChanged;

            // Formをマウスでドラッグして位置を移動するときは連続してLocationChangedイベントが発生する
            // 位置調整は移動が終わったあとに一度だけ行うように、タイマーで遅延させる
            _locationAdjustTimer = new System.Windows.Forms.Timer();
            _locationAdjustTimer.Interval = 3000;
            _locationAdjustTimer.Tick += LocationAdjustTimer_Tick;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _screenDpi = this.DeviceDpi;
            var location = Properties.Settings.Default.IsLocationSaved ?
                Properties.Settings.Default.Location :
                this.Location;
            UpdateWindow(location);
        }

        private Point AdjustLocation(Point currentLocation)
        {
            // 現在のスクリーンを取得
            var screen = Screen.FromControl(this);
            var wa = screen.WorkingArea;

            int newX = currentLocation.X;
            int newY = currentLocation.Y;

            // 左端・上端にはみ出ていたら修正
            if (newX < wa.Left) newX = wa.Left;
            if (newY < wa.Top) newY = wa.Top;

            // 右端・下端にはみ出ていたら修正
            if (newX + this.Width > wa.Right) newX = wa.Right - this.Width;
            if (newY + this.Height > wa.Bottom) newY = wa.Bottom - this.Height;
            var newLocation = new Point(newX, newY);
            Debug.WriteLine($"AdjustLocation {currentLocation} -> {newLocation}");
            return newLocation;
        }

        private Point AdjustLocationWithSize(Point location, Size currentSize, Size newSize)
        {
            // 現在のスクリーンを取得
            var screen = Screen.FromControl(this);
            var wa = screen.WorkingArea;

            var x = location.X;
            var leftSpace = location.X - wa.Left;
            var rightSpace = wa.Right - (location.X + currentSize.Width);
            if (Math.Abs(leftSpace) > Math.Abs(rightSpace))
            {
                x += currentSize.Width - newSize.Width;
            }

            var y = location.Y;
            var topSpace = location.Y - wa.Top;
            var bottomSpace = wa.Bottom - (location.Y + currentSize.Height);
            if (Math.Abs(topSpace) > Math.Abs(bottomSpace))
            {
                y += currentSize.Height - newSize.Height;
            }

            // 左端・上端にはみ出ていたら修正
            if (x < wa.Left)
            {
                x = wa.Left;
            }
            if (y < wa.Top)
            {
                y = wa.Top;
            }

            // 右端・下端にはみ出ていたら修正
            if (x + newSize.Width > wa.Right)
            {
                x = wa.Right - newSize.Width;
            }
            if (y + newSize.Height > wa.Bottom)
            {
                y = wa.Bottom - newSize.Height;
            }
            var newLocation = new Point(x, y);
            return newLocation;
        }

        private void SetLocationSafe(Point newLocation)
        {
            // 位置調整中に再帰的に位置調整が発生しないようにする
            _isAdjustingLocation = true;
            try
            {
                this.Location = newLocation;
            }
            finally
            {
                _isAdjustingLocation = false;
            }
        }

        private void UpdateWindow(Point location)
        {
            var bitmap = _drawingContext.CreateBitmap(this.DeviceDpi, _statuses);
            var newLocation = AdjustLocationWithSize(location, this.ClientSize, bitmap.Size);
            // 先にウィンドウサイズを変更してからレイヤードビットマップを更新する
            // そうしないと、ウィンドウサイズ変更時にちらつく
            // 最後に位置を調整する
            this.ClientSize = bitmap.Size;
            this.SetLayeredBitmap(bitmap);
            this.SetLocationSafe(newLocation);
        }

        private void OnBatteryStatusChanged(IReadOnlyCollection<BattStatus> statuses)
        {
            this.Invoke(() =>
            {
                _statuses = statuses;
                UpdateWindow(this.Location);
            });
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            // 位置調整中は無視
            if (_isAdjustingLocation)
            {
                return;
            }

            _locationAdjustTimer.Stop();
            _locationAdjustTimer.Start();
        }
        private void MainWindow_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            Debug.WriteLine("Display settings changed (screen configuration changed)");
            _locationAdjustTimer.Stop();
            _locationAdjustTimer.Start();
        }
        private void LocationAdjustTimer_Tick(object? sender, EventArgs e)
        {
            _locationAdjustTimer.Stop();
            if (_screenDpi == this.DeviceDpi)
            {
                var newLocation = this.AdjustLocation(this.Location);
                this.SetLocationSafe(newLocation);
            }
            else
            {
                Debug.WriteLine($"DPI changed: {_screenDpi} -> {this.DeviceDpi}");
                _screenDpi = this.DeviceDpi;
                this.UpdateWindow(this.Location);
            }
        }

        private async void MainWindow_FormClosing(object? sender, FormClosingEventArgs e)
        {
            Debug.WriteLine("MainWindow_FormClosing");
            // バックグラウンド処理の終了を待つため、FormClosingイベントをキャンセルしてウィンドウを非表示にする
            this.FormClosing -= MainWindow_FormClosing;
            SystemEvents.DisplaySettingsChanged -= MainWindow_DisplaySettingsChanged;

            _locationAdjustTimer.Stop();

            e.Cancel = true;
            this.Visible = false;
            Debug.WriteLine("Hid the window instead of closing it.");

            Properties.Settings.Default.Location = this.Location;
            Properties.Settings.Default.IsLocationSaved = true;
            Properties.Settings.Default.Save();
            Debug.WriteLine($"Saved. IsLocationSaved: {Properties.Settings.Default.IsLocationSaved}, Location: {Properties.Settings.Default.Location}");

            // ここで呼び出し側へ返る
            await Task.Yield();

            // FormClosingはキャンセルしたため、MainWindowは非表示にしたものの通常のメッセージループ中となる
            // バックグラウンドタスクの終了を待つ
            // await Task.Yield() は必要である。
            // すでに監視タスクが完了している場合 _monitor.StopWatchAsync() が即座に完了してしまい、
            // FormClosingイベントの処理が完了する前に this.Close() することになってしまう。
            await _monitor.StopMonitorAsync();

            // MainWindowを閉じる
            // FormClosingイベントは再度発生するが、イベントハンドラは削除されているため、ここでは無限ループしない
            this.Close();
            Debug.WriteLine("MainWindow_FormClosing ended");
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_DEVICECHANGE = 0x0219;
            const int DBT_DEVNODES_CHANGED = 0x0007;

            if (m.Msg == WM_DEVICECHANGE)
            {
                if ((int)m.WParam == DBT_DEVNODES_CHANGED)
                {
                    _monitor.ScanFast();
                }
            }
            base.WndProc(ref m);
        }
    }
}
