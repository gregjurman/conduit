using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;


namespace Conduit
{
    public partial class ProgressForm : Form
    {
        string host;
        string fileName;

        public ProgressForm()
        {
            InitializeComponent();
        }

        private void ProgressForm_Load(object sender, EventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();

            host = "";
            fileName = "";

            if (args.Length == 2)
            {
                host = args[1];
                fileName = "";
            }
            if (args.Length == 3)
            {
                host = args[1];
                fileName = args[2];
            }

        }
    }
}
