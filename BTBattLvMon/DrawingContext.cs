using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BTBattLvMon
{
    internal class DrawingContext
    {
        private readonly record struct BrushPair(Brush ShadowBrush, Brush TextBrush);

        private class StringMeasurer : IDisposable
        {
            private bool disposedValue;
            private Bitmap _bmp;
            private Font _font;
            private Graphics _g;

            public StringMeasurer(int dpi, Font font)
            {
                _bmp = new Bitmap(1, 1);
                _bmp.SetResolution(dpi, dpi);
                _font = font;
                _g = Graphics.FromImage(_bmp);
            }

            public SizeF Measure(string text)
            {
                var size = _g.MeasureString(text, _font);
                return size;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        _g.Dispose();
                        _bmp.Dispose();
                    }

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        private const string DEFAULT_DISPLAY_NAME = "モニター対象なし";
        private const string DEFAULT_BATTERY_LEVEL = " 100%";
        private const string FONT_FAMILY_NAME = "Meiryo";
        private const int DEFAULT_DPI = 96;

        private const float FONT_SIZE_IN_POINT = 9f;
        private const float LINE_HEIGHT_RATIO = 1.6f;

        private const float LEFT_MARGIN_IN_POINT = 2f;
        private const float NAME_MARGIN_POINT = 5f;
        private const float MAX_NAME_WIDTH_IN_POINT = 100f;
        private const float BATTERY_LEVEL_WIDTH_IN_POINT = 20f;
        private const float HANDLE_RADIUS_IN_POINT = 4f;
        private const float RIGHT_MARGIN_IN_POINT = 2f;

        private static readonly StringFormat VCENTER_NEAR = new()
        {
            Alignment = StringAlignment.Near,         // 水平方向（左揃え）
            LineAlignment = StringAlignment.Center,   // 垂直方向（中央揃え）
            Trimming = StringTrimming.EllipsisCharacter, // 省略記号を表示
            FormatFlags = StringFormatFlags.LineLimit, // 行が長すぎる場合に省略記号を表示
        };
        private static readonly StringFormat VCENTER_FAR = new()
        {
            Alignment = StringAlignment.Far,         // 水平方向（右揃え）
            LineAlignment = StringAlignment.Center,   // 垂直方向（中央揃え）
            Trimming = StringTrimming.EllipsisCharacter, // 省略記号を表示
            FormatFlags = StringFormatFlags.LineLimit, // 行が長すぎる場合に省略記号を表示
        };

        private const float OFFSET = 1f;
        private static readonly SizeF[] OFFSETS = new SizeF[]
        {
            new SizeF(-OFFSET, -OFFSET),
            new SizeF(-OFFSET, OFFSET),
            new SizeF(OFFSET, -OFFSET),
            new SizeF(OFFSET, OFFSET),
        };

        private readonly Color BACK_COLOR = Color.FromArgb(1, 128, 128, 128);
        private readonly BrushPair _normalBrushPair = new(new SolidBrush(Color.FromArgb(255, 192, 192, 192)), new SolidBrush(Color.FromArgb(255, 0, 0, 0)));
        private readonly BrushPair _aleartBrushPair = new(new SolidBrush(Color.FromArgb(255, 255, 255, 255)), new SolidBrush(Color.FromArgb(255, 128, 0, 0)));
        private int _dpi;
        private float _fontHeight;
        private float _lineHeight;
        private float _leftMargin;
        private float _nameWidth;
        private float _batteryLevelWidth;
        private float _rightMargin;
        private float _defaultWidth;

        public DrawingContext()
        {
        }

        private Rectangle GetWholeRect(int lineCount)
        {
            if (lineCount == 0)
            {
                var defaultRect = this.GetDefaultRect();
                var defaultWidth = (int)Math.Ceiling(_leftMargin + defaultRect.Width + _rightMargin);
                var defaultHeight = (int)Math.Ceiling(defaultRect.Height);
                return new Rectangle(0, 0, defaultWidth, defaultHeight);
            }
            var rectTemp = this.GetLineRect(0);
            var width = (int)Math.Ceiling(rectTemp.Right);
            var height = (int)Math.Ceiling(rectTemp.Height * lineCount);
            return new Rectangle(0, 0, width, height);
        }

        private float GetNameWidth(Graphics g, Font font, IReadOnlyCollection<BattStatus> statuses)
        {
            float nameWidth = 0f;
            foreach (var status in statuses)
            {
                var size = g.MeasureString(status.FriendlyName, font);
                nameWidth = Math.Max(nameWidth, size.Width);
            }
            nameWidth += NAME_MARGIN_POINT * _dpi / 72f;
            var maxNameWidth = MAX_NAME_WIDTH_IN_POINT * _dpi / 72f;
            nameWidth = Math.Min(nameWidth, maxNameWidth);
            return nameWidth;
        }

        private void CalcMetrics(Font font, IReadOnlyCollection<BattStatus> statuses)
        {
            using var tempBitmap = new Bitmap(1, 1);
            tempBitmap.SetResolution(_dpi, _dpi);
            using var g = Graphics.FromImage(tempBitmap);
            float pointToPix = _dpi / 72f;
            _leftMargin = LEFT_MARGIN_IN_POINT * pointToPix;
            _nameWidth = this.GetNameWidth(g, font, statuses);
            _batteryLevelWidth = g.MeasureString(DEFAULT_BATTERY_LEVEL, font).Width;
            _rightMargin = LEFT_MARGIN_IN_POINT * pointToPix;
            _defaultWidth = g.MeasureString(DEFAULT_DISPLAY_NAME, font).Width;
        }

        private RectangleF GetDefaultRect()
        {
            return new RectangleF(_leftMargin, 0, _defaultWidth, _lineHeight);
        }

        private RectangleF GetNameRect(int lineIndex)
        {
            var y = _lineHeight * lineIndex;
            return new RectangleF(_leftMargin, y, _nameWidth, _lineHeight);
        }
        private RectangleF GetBatteryLevelRect(int lineIndex)
        {
            var y = _lineHeight * lineIndex;
            return new RectangleF(_leftMargin + _nameWidth, y, _batteryLevelWidth, _lineHeight);
        }

        private RectangleF GetLineRect(int lineIndex)
        {
            var y = _lineHeight * lineIndex;
            var width = _leftMargin + _nameWidth + _batteryLevelWidth + _rightMargin;
            return new RectangleF(0, y, width, _lineHeight);
        }
        private void DrawOutlinedText(Graphics g, string text, Font font, BrushPair brushPair, RectangleF rect, StringFormat format)
        {
            foreach (var offset in OFFSETS)
            {
                var offsetRect = new RectangleF(rect.X + offset.Width, rect.Y + offset.Height, rect.Width, rect.Height);
                g.DrawString(text, font, brushPair.ShadowBrush, offsetRect, format);
            }
            g.DrawString(text, font, brushPair.TextBrush, rect, format);
        }

        public Bitmap CreateBitmap(int dpi, IReadOnlyCollection<BattStatus> statuses)
        {
            _dpi = dpi;
            float pointToPix = _dpi / 72f;
            _fontHeight = FONT_SIZE_IN_POINT * pointToPix;
            _lineHeight = _fontHeight * LINE_HEIGHT_RATIO;
            using var font = new Font(FONT_FAMILY_NAME, _fontHeight, FontStyle.Bold, GraphicsUnit.Pixel);

            this.CalcMetrics(font, statuses);
            var size = this.GetWholeRect(statuses.Count).Size;
            // bitmap の描画まで正常にできたら bitmapを戻り値として返すが、
            // 描画途中で例外が発生した場合は bitmap を破棄して例外を伝搬させる。
            // そもそも bitmap の生成に失敗した場合は破棄の必要はない。
            var bitmap = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                bitmap.SetResolution(_dpi, _dpi);

                using var g = Graphics.FromImage(bitmap);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; // アンチエイリアスを有効化
                                                                                       // g.Clear(Color.Transparent);
                g.Clear(BACK_COLOR);
                if (statuses.Count == 0)
                {
                    var defaultRect = this.GetDefaultRect();
                    DrawOutlinedText(g, DEFAULT_DISPLAY_NAME, font, _normalBrushPair, defaultRect, VCENTER_NEAR);
                }
                else
                {
                    int lineIndex = 0;
                    foreach (var status in statuses)
                    {
                        var batteryLevelText = $"{status.BatteryLevel}%";
                        if (status.BatteryLevel > 20)
                        {
                            var nameRect = this.GetNameRect(lineIndex);
                            DrawOutlinedText(g, status.FriendlyName, font, _normalBrushPair, nameRect, VCENTER_NEAR);
                            var batteryLevelRect = this.GetBatteryLevelRect(lineIndex);
                            DrawOutlinedText(g, batteryLevelText, font, _normalBrushPair, batteryLevelRect, VCENTER_FAR);
                        }
                        else
                        {
                            var lineRect = this.GetLineRect(lineIndex);
                            g.FillRectangle(_aleartBrushPair.ShadowBrush, lineRect);
                            var nameRect = this.GetNameRect(lineIndex);
                            g.DrawString(status.FriendlyName, font, _aleartBrushPair.TextBrush, nameRect, VCENTER_NEAR);
                            var batteryLevelRect = this.GetBatteryLevelRect(lineIndex);
                            g.DrawString(batteryLevelText, font, _aleartBrushPair.TextBrush, batteryLevelRect, VCENTER_FAR);
                        }
                        lineIndex++;
                    }
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
    }

}
