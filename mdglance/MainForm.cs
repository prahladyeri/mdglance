/*
 * @brief Main Form
 * 
 * @author Prahlad Yeri <prahladyeri@yahoo.com>
 * @license MIT
 */
using Markdig;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace mdglance
{
    public partial class MainForm : Form
    {

        public MainForm()
        {
            InitializeComponent();
            webBrowser1.Navigating += WebBrowser1_Navigating;
        }

        private void WebBrowser1_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.ToString() != "about:blank")
            {
                e.Cancel = true;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            //Console.WriteLine(result);   // prints: <p>This is a text with some <em>emphasis</em></p>
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Markdown files (*.md)|*.md";
            DialogResult res =  ofd.ShowDialog();
            //MessageBox.Show(ofd.FileName);

            string md = File.ReadAllText(ofd.FileName);
            // Quick pre-parser fix: finds blank lines trapped inside table rows and collapses them
            string sanitizedMd = Regex.Replace(md, @"(\|\s*\r?\n)\s*\r?\n(\s*\|)", "$1$2");

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var htmlContent = Markdown.ToHtml(sanitizedMd, pipeline);
            string completeHtml = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
                    <meta charset=""utf-8"" />
                    <style>
                        body {{
                            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                            font-size: 14px;
                            line-height: 1.6;
                            color: #333;
                            padding: 20px;
                        }}

                        /* Clean up table layouts, borders, and alternating rows */
                        table {{
                            border-collapse: collapse;
                            width: 100%;
                            margin-bottom: 16px;
                        }}
                        table th, table td {{
                            padding: 6px 13px;
                            border: 1px solid #dfe2e5;
                        }}
                        table tr:nth-child(even) {{
                            background-color: #f6f8fa;
                        }}
                        table th {{
                            font-weight: 600;
                            background-color: #f6f8fa;
                        }}
                        code {{
                            background-color: rgba(27,31,35,0.05);
                            padding: 0.2em 0.4em;
                            border-radius: 3px;
                            font-family: Consolas, 'Liberation Mono', Menlo, monospace;
                            font-size: 85%;
                        }}
                        pre {{
                            background-color: #f6f8fa;
                            padding: 16px;
                            border-radius: 3px;
                            overflow: auto;
                        }}
                        pre code {{
                            background-color: transparent;
                            padding: 0;
                        }}
                    </style>
                </head>
                <body>
                    {htmlContent}
                </body>
                </html>";
            webBrowser1.DocumentText = completeHtml;

            this.Text = Application.ProductName + " - " + ofd.FileName;
        }
    }
}