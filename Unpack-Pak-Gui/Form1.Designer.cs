namespace Unpack_Pak_Gui
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private Button btnBrowseSource;
        private Button btnBrowseDestination;
        private Button btnExtract;
        private Label lblSourcePath;
        private Label lblDestinationPath;
        private ListView lvFiles;
        private ListBox lstFiles; // Change to ListBox


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


        // Initialize your components
        private void InitializeComponent()
        {
            btnBrowseSource = new Button();
            btnBrowseDestination = new Button();
            btnExtract = new Button();
            lblSourcePath = new Label();
            lblDestinationPath = new Label();
            lstFiles = new ListBox();
            SuspendLayout();
            // 
            // btnBrowseSource
            // 
            btnBrowseSource.Location = new Point(757, 27);
            btnBrowseSource.Name = "btnBrowseSource";
            btnBrowseSource.Size = new Size(120, 30);
            btnBrowseSource.TabIndex = 4;
            btnBrowseSource.Text = "Browse Source";
            btnBrowseSource.Click += BtnBrowseSource_Click;
            // 
            // btnBrowseDestination
            // 
            btnBrowseDestination.Location = new Point(757, 70);
            btnBrowseDestination.Name = "btnBrowseDestination";
            btnBrowseDestination.Size = new Size(120, 30);
            btnBrowseDestination.TabIndex = 2;
            btnBrowseDestination.Text = "Browse Destination";
            btnBrowseDestination.Click += BtnBrowseDestination_Click;
            // 
            // btnExtract
            // 
            btnExtract.Location = new Point(757, 735);
            btnExtract.Name = "btnExtract";
            btnExtract.Size = new Size(120, 30);
            btnExtract.TabIndex = 0;
            btnExtract.Text = "Extract";
            btnExtract.Click += BtnExtract_Click;
            // 
            // lblSourcePath
            // 
            lblSourcePath.AutoSize = true;
            lblSourcePath.Location = new Point(12, 35);
            lblSourcePath.Name = "lblSourcePath";
            lblSourcePath.Size = new Size(88, 15);
            lblSourcePath.TabIndex = 5;
            lblSourcePath.Text = "No file selected";
            // 
            // lblDestinationPath
            // 
            lblDestinationPath.AutoSize = true;
            lblDestinationPath.Location = new Point(12, 85);
            lblDestinationPath.Name = "lblDestinationPath";
            lblDestinationPath.Size = new Size(131, 15);
            lblDestinationPath.TabIndex = 3;
            lblDestinationPath.Text = "No destination selected";
            // 
            // lstFiles
            // 
            lstFiles.ItemHeight = 15;
            lstFiles.Location = new Point(12, 130);
            lstFiles.Name = "lstFiles";
            lstFiles.ScrollAlwaysVisible = true;
            lstFiles.Size = new Size(847, 589);
            lstFiles.TabIndex = 1;
            // 
            // Form1
            // 
            ClientSize = new Size(889, 777);
            Controls.Add(btnExtract);
            Controls.Add(lstFiles);
            Controls.Add(btnBrowseDestination);
            Controls.Add(lblDestinationPath);
            Controls.Add(btnBrowseSource);
            Controls.Add(lblSourcePath);
            Name = "Form1";
            Text = "File Unpacker";
            ResumeLayout(false);
            PerformLayout();
        }

        // Add file names to ListBox




        #endregion
    }
}
