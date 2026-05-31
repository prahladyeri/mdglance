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
        private string _currentRootDirectory = @"";

        public MainForm()
        {
            InitializeComponent();
            webBrowser1.Navigating += WebBrowser1_Navigating;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            //Console.WriteLine(result);   // prints: <p>This is a text with some <em>emphasis</em></p>
            //InitializeDirectoryTree(Directory.GetCurrentDirectory());
            //InitializeDirectoryTree(@"D:\docs\ai_chat");
            InitializeSystemDrives(); // Initialize our standard drive roots first

            // 2. Check if a file path was passed via "Open With"
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                string incomingFilePath = args[1];
                if (File.Exists(incomingFilePath) && incomingFilePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    // Auto-navigate the sidebar and render the document
                    AutoBrowseToPath(incomingFilePath);
                }
            }
        }

        private void InitializeSystemDrives() {
            try
            {
                treeView1.BeginUpdate(); // Freeze UI painting temporarily for a smooth load
                treeView1.Nodes.Clear();

                // Fetch all logical drives currently mapped to the operating system
                string[] drives = Environment.GetLogicalDrives();

                foreach (string drive in drives)
                {
                    DriveInfo di = new DriveInfo(drive);

                    // Skip unready drives (like empty CD-ROM slots or disconnected network shares)
                    if (!di.IsReady) continue;

                    // Create a root node for each drive (e.g., "Local Disk (C:)")
                    string nodeText = string.IsNullOrEmpty(di.VolumeLabel)
                        ? $"Local Disk ({drive.TrimEnd('\\')})"
                        : $"{di.VolumeLabel} ({drive.TrimEnd('\\')})";

                    TreeNode driveNode = new TreeNode(nodeText) { Tag = drive };

                    // Add our dummy node so the [+] expansion box appears
                    driveNode.Nodes.Add(new TreeNode("Loading..."));
                    treeView1.Nodes.Add(driveNode);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing system drives: {ex.Message}");
            }
            finally
            {
                treeView1.EndUpdate(); // Resume normal layout painting
            }
        }

        private void AutoBrowseToPath(string fullPath)
        {
            try
            {
                treeView1.BeginUpdate();

                // 1. Extract the file's root directory info
                FileInfo fileInfo = new FileInfo(fullPath);
                string directoryPath = fileInfo.DirectoryName;
                string fileName = fileInfo.Name;

                // Split the directory path into separate folder names (accounting for drive letters)
                // e.g., "D:\Projects\Docs" becomes ["D:\", "Projects", "Docs"]
                string[] pathSegments = directoryPath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length == 0) return;

                // Clean up the drive segment format to match our Environment path string
                if (!pathSegments[0].EndsWith("\\"))
                {
                    pathSegments[0] += "\\";
                }

                TreeNodeCollection currentNodes = treeView1.Nodes;
                TreeNode targetNode = null;

                // 2. Drill down through folder nodes segment by segment
                foreach (string segment in pathSegments)
                {
                    bool matchFound = false;
                    foreach (TreeNode node in currentNodes)
                    {
                        // Match by path checking stored in the node's Tag
                        string nodePath = node.Tag?.ToString() ?? "";

                        // For the drive root, match exact path; for subfolders, match the name string
                        if (string.Equals(nodePath, segment, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(node.Text, segment, StringComparison.OrdinalIgnoreCase))
                        {
                            targetNode = node;

                            // Force lazy load if the dummy node is still present
                            if (targetNode.Nodes.Count == 1 && targetNode.Nodes[0].Text == "Loading...")
                            {
                                targetNode.Nodes.Clear();
                                PopulateDirectory(new DirectoryInfo(nodePath), targetNode.Nodes);
                            }

                            targetNode.Expand();
                            currentNodes = targetNode.Nodes;
                            matchFound = true;
                            break;
                        }
                    }
                    if (!matchFound) return; // Stop if the path breaks or folder is missing
                }

                // 3. Find and select the actual markdown file node within the final folder view
                if (currentNodes != null)
                {
                    foreach (TreeNode fileNode in currentNodes)
                    {
                        if (string.Equals(fileNode.Text, fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            treeView1.SelectedNode = fileNode; // Highlight it in UI
                            fileNode.EnsureVisible();          // Auto-scroll sidebar viewport to view it
                            LoadAndRenderMarkdown(fullPath);   // Parse and push to browser window
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Auto-navigation failed: {ex.Message}", "Navigation Error");
            }
            finally
            {
                treeView1.EndUpdate();
            }
        }

        // Call this method when a user selects a working folder 
        // (via a FolderBrowserDialog or dropped directory path)
        private void InitializeDirectoryTree(string targetPath)
        {
            _currentRootDirectory = targetPath;
            treeView1.Nodes.Clear();

            DirectoryInfo rootDir = new DirectoryInfo(targetPath);
            TreeNode rootNode = new TreeNode(rootDir.Name) { Tag = rootDir.FullName };

            treeView1.Nodes.Add(rootNode);
            PopulateDirectory(rootDir, rootNode.Nodes);
            rootNode.Expand();
        }

        // Populates a single level of directories and markdown files
        private void PopulateDirectory(DirectoryInfo dir, TreeNodeCollection nodeCollection)
        {
            try
            {
                // 1. Fetch subdirectories and add a dummy node to allow expansion
                foreach (DirectoryInfo subDir in dir.GetDirectories())
                {
                    TreeNode dirNode = new TreeNode(subDir.Name) { Tag = subDir.FullName };
                    dirNode.Nodes.Add(new TreeNode("Loading...")); // Dummy node
                    nodeCollection.Add(dirNode);
                }

                // 2. Fetch only Markdown files to keep the panel focused
                foreach (FileInfo file in dir.GetFiles("*.md"))
                {
                    TreeNode fileNode = new TreeNode(file.Name) { Tag = file.FullName };
                    nodeCollection.Add(fileNode);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Safely ignore system folders or protected directories you don't have access to
            }
        }


        private void WebBrowser1_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.ToString() != "about:blank")
            {
                e.Cancel = true;
            }
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
            if (res != DialogResult.OK) return;
            //LoadAndRenderMarkdown(ofd.FileName);
            AutoBrowseToPath(ofd.FileName);
        }

        // Event handler: Fires when a user clicks the "+" expansion box on a folder node
        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode expandingNode = e.Node;

            // If the folder has our dummy node inside, clear it and lazy-load real paths
            if (expandingNode.Nodes.Count == 1 && expandingNode.Nodes[0].Text == "Loading...")
            {
                expandingNode.Nodes.Clear();
                string fullPath = expandingNode.Tag.ToString();
                PopulateDirectory(new DirectoryInfo(fullPath), expandingNode.Nodes);
            }

        }

        // Event handler: Fires when a user clicks an item in the sidebar list
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string selectedPath = e.Node.Tag.ToString();

            // Check if it's an actual markdown file, then read and render it
            if (File.Exists(selectedPath) && selectedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                LoadAndRenderMarkdown(selectedPath);
            }
        }

        private void LoadAndRenderMarkdown(string filePath)
        {
            try
            {
                string md = File.ReadAllText(filePath);
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
                            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Segoe UI Emoji', 'Segoe UI Symbol', Helvetica, Arial, sans-serif;
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
                //webBrowser1.DocumentText = completeHtml;
                if (webBrowser1.Document == null)
                {
                    webBrowser1.Navigate("about:blank");
                    // Force WinForms to pump messages and finalize browser initialization
                    Application.DoEvents();
                }
                webBrowser1.Document.OpenNew(true);
                webBrowser1.Document.Write(completeHtml);
                webBrowser1.Refresh();
                this.Text = Application.ProductName + " - " + filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading file: {ex.Message}");
            }
        }

        private void exitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}