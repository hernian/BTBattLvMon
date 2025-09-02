namespace BTBattLvMon
{
    partial class FormMain
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            timerUpdate = new System.Windows.Forms.Timer(components);
            listViewInfo = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            SuspendLayout();
            // 
            // timerUpdate
            // 
            timerUpdate.Tick += TimerUpdate_Tick;
            // 
            // listViewInfo
            // 
            listViewInfo.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2 });
            listViewInfo.Enabled = false;
            listViewInfo.Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 128);
            listViewInfo.Location = new Point(111, 65);
            listViewInfo.Name = "listViewInfo";
            listViewInfo.Scrollable = false;
            listViewInfo.Size = new Size(185, 205);
            listViewInfo.TabIndex = 0;
            listViewInfo.UseCompatibleStateImageBehavior = false;
            listViewInfo.View = View.Details;
            // 
            // columnHeader1
            // 
            columnHeader1.Width = 120;
            // 
            // columnHeader2
            // 
            columnHeader2.TextAlign = HorizontalAlignment.Right;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(listViewInfo);
            FormBorderStyle = FormBorderStyle.None;
            Name = "FormMain";
            Opacity = 0.5D;
            Text = "BTBattMon";
            FormClosed += FormMain_FormClosed;
            Load += FormMain_Load;
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Timer timerUpdate;
        private ListView listViewInfo;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
    }
}
