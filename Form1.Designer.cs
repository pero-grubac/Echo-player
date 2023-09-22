namespace EchoPlayer
{
    partial class Form1
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
            filePathLabel = new Label();
            delayLabel = new Label();
            filePathTxtBox = new TextBox();
            browseButton = new Button();
            delayTxtBox = new TextBox();
            downloadButton = new Button();
            SuspendLayout();
            // 
            // filePathLabel
            // 
            filePathLabel.AutoSize = true;
            filePathLabel.Location = new Point(19, 52);
            filePathLabel.Name = "filePathLabel";
            filePathLabel.Size = new Size(60, 20);
            filePathLabel.TabIndex = 0;
            filePathLabel.Text = "FilePath";
            // 
            // delayLabel
            // 
            delayLabel.AutoSize = true;
            delayLabel.Location = new Point(19, 105);
            delayLabel.Name = "delayLabel";
            delayLabel.Size = new Size(67, 20);
            delayLabel.TabIndex = 1;
            delayLabel.Text = "Delay (s)";
            // 
            // filePathTxtBox
            // 
            filePathTxtBox.Location = new Point(98, 45);
            filePathTxtBox.Name = "filePathTxtBox";
            filePathTxtBox.Size = new Size(125, 27);
            filePathTxtBox.TabIndex = 2;
            // 
            // browseButton
            // 
            browseButton.Location = new Point(264, 45);
            browseButton.Name = "browseButton";
            browseButton.Size = new Size(94, 29);
            browseButton.TabIndex = 3;
            browseButton.Text = "Browse";
            browseButton.UseVisualStyleBackColor = true;
            browseButton.Click += browseButton_Click;
            // 
            // delayTxtBox
            // 
            delayTxtBox.Location = new Point(98, 98);
            delayTxtBox.Name = "delayTxtBox";
            delayTxtBox.Size = new Size(125, 27);
            delayTxtBox.TabIndex = 5;
            delayTxtBox.TextChanged += delayTxtBox_TextChanged;
            // 
            // downloadButton
            // 
            downloadButton.Location = new Point(264, 101);
            downloadButton.Name = "downloadButton";
            downloadButton.Size = new Size(94, 29);
            downloadButton.TabIndex = 6;
            downloadButton.Text = "Download";
            downloadButton.UseVisualStyleBackColor = true;
            downloadButton.Click += downloadButton_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(412, 207);
            Controls.Add(downloadButton);
            Controls.Add(delayTxtBox);
            Controls.Add(browseButton);
            Controls.Add(filePathTxtBox);
            Controls.Add(delayLabel);
            Controls.Add(filePathLabel);
            Name = "Form1";
            Text = "EchoPlayer";
            FormClosed += Form1_FormClosed;
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label filePathLabel;
        private Label delayLabel;
        private TextBox filePathTxtBox;
        private Button browseButton;
        private TextBox delayTxtBox;
        private Button downloadButton;
    }
}