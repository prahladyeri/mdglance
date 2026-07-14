/*
 * @brief Main Form
 * 
 * @author Prahlad Yeri <prahladyeri@yahoo.com>
 * @license MIT
 * @date 2026-05-31
 */
using Markdig;
using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Linq;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using Markdig.Extensions.AutoIdentifiers;
using mdglance.Helpers;
using Newtonsoft.Json;
using System.Text;

namespace mdglance
{
    public partial class MainForm : Form
    {
        private WebView2 webView21;
        private bool _isAutoNavigating = false;

        public MainForm()
        {
            InitializeComponent();
            SetApplicationIcon();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            openToolStripMenuItem.Image = imageList1.Images["folder-open"];
            exitToolStripMenuItem.Image = imageList1.Images["exit"];
            aboutToolStripMenuItem.Image = imageList1.Images["help-browser"];
            InitializeSystemDrives();
            try
            {
                webView21.CoreWebView2InitializationCompleted += WebView21_CoreWebView2InitializationCompleted;

                // Clean up the application folder by routing the UDF to %LocalAppData%
                //string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string cacheFolder = Path.Combine(Program.LocalAppData, "WebView2Profile");
                string browserArgs = "--disable-features=OverscrollHistoryNavigation --disable-features=ElasticOverscroll";
                var environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null, 
                    userDataFolder: cacheFolder,
                    options: new CoreWebView2EnvironmentOptions(browserArgs)
                );

                await webView21.EnsureCoreWebView2Async(environment);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Chromium rendering subsystem: {ex.Message}", "Runtime Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            //Initialize splitter
            splitContainer1.SplitterDistance = Program.Settings.SplitterPosition;
        }

        private void WebView21_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            string assetsPath = Path.Combine(
                Application.StartupPath,
                "assets"
            );

            webView21.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets",
                assetsPath,
                CoreWebView2HostResourceAccessKind.Allow
            );

            webView21.DefaultBackgroundColor = Color.FromArgb(246, 248, 250);
            webView21.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;

            //TODO: BUILD
            webView21.CoreWebView2.Settings.AreDevToolsEnabled = true;

            // Lock Down Chromium Core Environment Security & Context Menus
            webView21.CoreWebView2.Settings.IsScriptEnabled = true;
            webView21.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView21.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Register Native WebView2 Core Interceptors
            webView21.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView21.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            webView21.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView21.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            //webView21.CoreWebView2.NavigateToString(LoaderHtml);
            //Application.DoEvents();

            // Init the default file
            string[] args = Environment.GetCommandLineArgs();
            string filePath = "";
            if (args.Length > 1)
            {
                filePath = args[1];
            }
            else
            {
                filePath = Program.Settings.LastOpened;
            }
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                AutoBrowseToPath(filePath); // Auto-navigate the sidebar and render the document
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            lblStatus.Text = "Ready";
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // Parse message string safely without heavy JSON dependencies
                string rawMessage = e.WebMessageAsJson;
                dynamic msg = JsonConvert.DeserializeObject(rawMessage);

                if (msg.type == "hover")
                {
                    // Basic regex extraction for lightweight parsing performance
                    var match = Regex.Match(rawMessage, @"\""url\"":\""([^""]+)\""");
                    if (match.Success)
                    {
                        lblStatus.Text = match.Groups[1].Value + " (Shift + Click to copy target link)";
                    }
                }
                
                else if (msg.type == "copy")
                {
                    Clipboard.SetText((string)msg.text);
                }
                else if (msg.type == "clear")
                {
                    lblStatus.Text = "Ready";
                }
                else if (msg.type == "copyShortcut")
                {
                    var match = Regex.Match(rawMessage, @"\""url\"":\""([^""]+)\""");
                    if (match.Success)
                    {
                        string targetUrl = match.Groups[1].Value;
                        Clipboard.SetText(targetUrl);
                        lblStatus.Text = $"Copied link target: {targetUrl}";
                    }
                }
            }
            catch
            {
                // Safeguard against message channel corruption
            }
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // Halt native external navigation pipeline actions completely
            e.Handled = true;

            string targetUrl = e.Uri;
            if (!string.IsNullOrEmpty(targetUrl))
            {
                try
                {
                    Clipboard.SetText(targetUrl);
                    lblStatus.Text = $"Copied link target: {targetUrl}";
                }
                catch (Exception ex)
                {
                    lblStatus.Text = $"Failed to copy link: {ex.Message}";
                }
            }
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            string targetUrl = e.Uri;

            // Allow initial bootstrapping or explicit in-memory string injections
            if (targetUrl.Equals("about:blank", StringComparison.OrdinalIgnoreCase) ||
                targetUrl.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Cancel any external link navigation attempts to keep application context sandboxed
            //e.Cancel = true;
            Uri uri = new Uri(e.Uri);
            if (! uri.IsFile) {
                return;
            }
        }

        private void SetApplicationIcon()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                this.Icon = Icon.ExtractAssociatedIcon(exePath);
            }
            catch
            {
                // Fallback gracefully to default system graphics if running in a restricted sandbox
            }
        }

        private void InitializeSystemDrives() {
            try
            {
                treeView1.BeginUpdate();
                treeView1.Nodes.Clear();
                string[] drives = Environment.GetLogicalDrives();

                foreach (string drive in drives)
                {
                    DriveInfo di = new DriveInfo(drive);
                    if (!di.IsReady) continue;

                    string nodeText = string.IsNullOrEmpty(di.VolumeLabel)
                        ? $"Local Disk ({drive.TrimEnd('\\')})"
                        : $"{di.VolumeLabel} ({drive.TrimEnd('\\')})";

                    TreeNode driveNode = new TreeNode(nodeText) { Tag = drive };
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
                treeView1.EndUpdate();
            }
        }

        private void AutoBrowseToPath(string fullPath)
        {
            try
            {
                treeView1.BeginUpdate();
                FileInfo fileInfo = new FileInfo(fullPath);
                string directoryPath = fileInfo.DirectoryName;
                string fileName = fileInfo.Name;

                string[] pathSegments = directoryPath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length == 0) return;

                //TODO: This works for standard Windows paths but will silently fail on UNC paths (\\server\share\file.md)
                //or paths with trailing separators.
                //Not critical for a local file viewer but worth hardening with Path.GetPathRoot() instead.
                if (!pathSegments[0].EndsWith("\\"))
                {
                    pathSegments[0] += "\\";
                }

                TreeNodeCollection currentNodes = treeView1.Nodes;
                TreeNode targetNode = null;

                foreach (string segment in pathSegments)
                {
                    bool matchFound = false;
                    foreach (TreeNode node in currentNodes)
                    {
                        string nodePath = node.Tag?.ToString() ?? "";

                        if (string.Equals(nodePath, segment, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(node.Text, segment, StringComparison.OrdinalIgnoreCase))
                        {
                            targetNode = node;

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
                    if (!matchFound) return;
                }

                if (currentNodes != null)
                {
                    foreach (TreeNode fileNode in currentNodes)
                    {
                        if (string.Equals(fileNode.Text, fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            _isAutoNavigating = true;
                            
                            treeView1.SelectedNode = fileNode;
                            fileNode.EnsureVisible();
                            
                            _isAutoNavigating = false;

                            LoadAndRenderMarkdown(fullPath);
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


        private void PopulateDirectory(DirectoryInfo dir, TreeNodeCollection nodeCollection)
        {
            try
            {
                var sortedDirectories = dir.GetDirectories().OrderByDescending(d => d.LastWriteTime);
                foreach (DirectoryInfo subDir in sortedDirectories)
                {
                    TreeNode dirNode = new TreeNode(subDir.Name) { Tag = subDir.FullName };
                    dirNode.ImageIndex = 0;
                    dirNode.SelectedImageIndex = 1;
                    dirNode.Nodes.Add(new TreeNode("Loading..."));
                    nodeCollection.Add(dirNode);
                }

                string[] allowedExtensions = { "*.md", "*.html", "*.htm", "*.txt" };
                var sortedFiles = allowedExtensions
                            .SelectMany(extension => dir.GetFiles(extension))
                            .OrderByDescending(f => f.LastWriteTime);

                foreach (FileInfo file in sortedFiles)
                {
                    TreeNode fileNode = new TreeNode(file.Name) { Tag = file.FullName };
                    fileNode.ImageIndex = 2;
                    fileNode.SelectedImageIndex = 2;
                    nodeCollection.Add(fileNode);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Safely ignore system folders or protected directories you don't have access to
            }
        }

        private void HtmlDocument_MouseLeave(object sender, HtmlElementEventArgs e)
        {
            // Instantly wipe the text label clear the moment the user's cursor exits a text target area
            lblStatus.Text = "Ready";
        }


        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Supported Files (*.md;*.html;*.htm)|*.md;*.html;*.htm|Markdown files (*.md)|*.md|HTML files (*.html;*.htm)|*.html;*.htm";
            DialogResult res =  ofd.ShowDialog();
            if (res != DialogResult.OK) return;
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
            if (_isAutoNavigating) return;

            string selectedPath = e.Node.Tag.ToString();

            if (File.Exists(selectedPath))
            {
                LoadAndRenderMarkdown(selectedPath);
            }
        }

        private void LoadAndRenderMarkdown(string filePath)
        {
            try
            {
                this.Text = Application.ProductName + " - " + filePath;
                lblStatus.Text = "Processing Markdown...";
                //webView21.CoreWebView2.NavigateToString(LoaderHtml);
                //Application.DoEvents();
                string bodyContent = "";


                if (filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) 
                {
                    bodyContent = File.ReadAllText(filePath);
                    bodyContent = System.Net.WebUtility.HtmlEncode(bodyContent);
                    bodyContent = $"<pre>{bodyContent}</pre>";
                }
                else if (filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                    filePath.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                {
                    bodyContent = File.ReadAllText(filePath);
                }
                else if (filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    string md = File.ReadAllText(filePath);

                    // TODO: This fixes a specific blank-line-in-table edge case but could silently corrupt other content patterns
                    string sanitizedMd = Regex.Replace(md, @"(\|\s*\r?\n)\s*\r?\n(\s*\|)", "$1$2");

                    var autoIdOptions = AutoIdentifierOptions.GitHub;
                    var pipeline = new MarkdownPipelineBuilder()
                        .UseAutoIdentifiers(autoIdOptions)
                        .UseAdvancedExtensions()
                        .DisableHtml()
                        .Build();

                    bodyContent = Markdown.ToHtml(sanitizedMd, pipeline);
                }

                var scriptToInject = @"
                    console.log('Current state:', document.readyState);

                    document.addEventListener('DOMContentLoaded', function() {
                        console.log('DOMContentLoaded done.');
                        document.querySelectorAll('.wait-for').forEach(el => el.classList.add('d-none'));
                        hljs.highlightAll();
                        document.querySelectorAll(""pre"").forEach(pre => {
                            const btn = document.createElement(""button"");
                            btn.innerText = ""📋"";
                            btn.className = ""copy-btn"";

                            btn.onclick = () => {
                                let code = pre.querySelector(""code"");
                                chrome.webview.postMessage({
                                    type: ""copy"",
                                    text: code.innerText
                                });


                                btn.innerText = ""Copied!"";
                                setTimeout(() => {
                                    btn.innerText = ""📋"";
                                }, 1500);
                            };

                            pre.style.position = ""relative"";

                            pre.appendChild(btn);
                        });
                    });

                    document.addEventListener('mouseover', function(e) {
                        let element = e.target;
                        while (element && element.tagName !== 'A') {
                            element = element.parentElement;
                        }
                        if (element) {
                            let url = element.href || element.getAttribute('href') || '';
                            if (url.startsWith('about:blank')) {
                                url = url.replace('about:blank', '');
                            }
                            window.chrome.webview.postMessage({ type: 'hover', url: url });
                        }
                    });

                    document.addEventListener('mouseout', function(e) {
                        let element = e.target;
                        while (element && element.tagName !== 'A') {
                            element = element.parentElement;
                        }
                        if (element) {
                            window.chrome.webview.postMessage({ type: 'clear' });
                        }
                    });

                    // Handle clicks, smooth-scrolling, and intercept Shift+Click modifiers cleanly
                    document.addEventListener('click', function(e) {
                        console.log('now entering');
                        let element = e.target;
                        while (element && element.tagName !== 'A') {
                            element = element.parentElement;
                        }
                        console.log('now evaluating');
                        if (element) {
                            let hrefAttr = element.getAttribute('href') || '';
                            console.log('entered', hrefAttr);
                            if (e.shiftKey) {
                                e.preventDefault();
                                let absoluteUrl = hrefAttr.startsWith('#') ? (window.location.href.split('#')[0] + hrefAttr) : element.href;
                                window.chrome.webview.postMessage({ type: 'copyShortcut', url: absoluteUrl });
                                return;
                            }
                            if (hrefAttr.startsWith('#')) {
                                e.preventDefault();
                                let targetId = hrefAttr.substring(1);
                                console.log('targetId', targetId);

                                let targetEl = document.getElementById(targetId) || 
                                                   document.getElementById(decodeURIComponent(targetId));

                                if (!targetEl) {
                                    // Regex matches leading numbers followed by a hyphen (e.g., ""2-"", ""10-"")
                                    //let cleanId = targetId.replace(/^\d+-/, '');
                                    let cleanId = targetId.replace(/^(\d+-|\d+\.-|section-\d+-)/i, '');
                                    console.log('Normalized targetId for Markdig:', cleanId);
        
                                    targetEl = document.getElementById(cleanId) || 
                                               document.getElementById(decodeURIComponent(cleanId)) ||
                                               document.querySelector(`[name='${cleanId}']`);
                                }

                                //let targetEl = document.getElementById(targetId) || document.getElementById(decodedId);
                                console.log('targetEl', targetEl);
                                if (targetEl) {
                                    targetEl.scrollIntoView({ behavior: 'smooth', block: 'start' });
                                    // Explicitly update window location context so state tracks nicely
                                    window.location.hash = hrefAttr;
                                }
                            }
                        }
                    });
                ";


                string secureOuterShell = @"<!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset=""utf-8"" />
                        <link rel=""stylesheet"" href=""https://appassets/github.min.css"">
                        <script src=""https://appassets/highlight.min.js""></script>

                        <style>
                        :root {
                            --main-font: 'Sitka Text', 'Bahnschrift', 'Segoe UI', sans-serif;
                            --heading-font: Bahnschrift, 'Sitka Text', 'Segoe UI', sans-serif;
                            --bgcolor: #f5f5f5;
	                        --fgcolor: #505050;
                            --linkcolor: #1a237e;
                        }
                        .copy-btn {
                            position: absolute;
                            font-family: 'cascadia code';
                            top: 8px;
                            right: 8px;
                            cursor: pointer;
                            background: rgba(255,255,255,0.8);
                            border: 1px solid #d0d7de;
                            border-radius: 6px;
                            padding: 4px 6px;
                            font-size: 14px;
                            line-height: 1;
                            /* opacity: 0; hide until hover 
                            transition: opacity 0.2s;*/
                        }
                        pre:hover.copy-btn {
                            opacity: 1; /* only show on hover - less visual noise */
                        }
                        .copy-btn:hover {
                            background: #f3f4f6;
                        }
                        body {
                                font-family: var(--main-font);
                                font-size: 18px;
                                color: var(--fgcolor); 
                                background-color: var(--bgcolor);
                                padding: 24px 32px;
                                /*max-width: 850px; */
                                margin: 0 auto;
                            }

                            blockquote {
                                border-left: 3px solid #d0d7de;
                                margin: 0 0 16px 0;
                                padding: 0 16px;
                            }
                            p, ul, ol, blockquote, table, pre {
                                margin-top: 0;
                                margin-bottom: 16px;
                            }
                            h1, h2, h3, h4 {
                                line-height: 1.25;
                                margin-top: 16px;
                                margin-bottom: 16px;
                            }

                            /* Override bold rendering for Segoe UI */
                            b, strong,
                            h1, h2, h3, h4, h5, h6,
                            th {
                                /* font-weight: 400; */
                            }

                            h1:first-child, h2:first-child { margin-top: 0; }
                            h1 { font-size: 24px; padding-bottom: 0.3em; border-bottom: 1px solid #eaecef; }
                            h2 { font-size: 24px; padding-bottom: 0.3em; border-bottom: 1px solid #eaecef; }
                            h3 { font-size: 24px; }
                            h4 { font-size: 24px; }
                            a:link, a:visited {
                                color: var(--linkcolor);
                                text-decoration: underline;
                            }

                            ::-webkit-scrollbar {width: 10px;
                            }
                            ::-webkit-scrollbar-track {background: #f6f8fa;
                            }
                            ::-webkit-scrollbar-thumb {background: #cdced0;
                                border-radius: 5px;
                            }
                            ::-webkit-scrollbar-thumb:hover {background: #a6a7a9;
                            }

                            table { border-collapse: collapse; width: 100%; margin-bottom: 16px; }
                            table th, table td { padding: 6px 13px; border: 1px solid #dfe2e5; }


                            pre {
                                white-space: pre-wrap;       /* Leaves spaces/newlines intact, but wraps lines when boundaries are hit */
                                word-wrap: break-word;       /* Forces long, unbroken text strings (like logs or long variables) to break */
                                overflow-wrap: break-word;   /* modern spec */
                                background-color: #f6f8fa; 
                                padding: 3px; 
                                border-radius: 3px;
                                border: 0.5px solid #9E9E9E;
                            }
                            code, pre {
                                font-family: 'Cascadia Code', Consolas, 'Courier New', monospace;
                            }
                            code { 
                                background-color: rgba(27,31,35,0.05); 
                                padding: 0.2em 0.4em; 
                                border-radius: 3px; 
                                font-size: 85%; 
                                }
                            pre code { 
                                background-color: transparent; 
                                padding: 2px !important; 
                            }

                            /* Fixed Overlay Viewport Centering Context */
                            .loader-container {
                                position: fixed;
                                top: 0;
                                left: 0;
                                width: 100vw;
                                height: 100vh;
                                background-color: var(--bgcolor); /* Blocks out unfinished DOM layouts cleanly */
                                color: var(--fgcolor);
                                display: flex;
                                flex-direction: column;
                                justify-content: center;
                                align-items: center;
                                z-index: 9999; /* Stays above text layout passes */
                            }

                            .hourglass {
                                font-size: 48px;
                                margin-bottom: 16px;
                                animation: flip 2s infinite ease-in-out;
                                display: inline-block;
                            }
                            @keyframes flip {
                                0% { transform: rotate(0deg); }
                                50% { transform: rotate(180deg); }
                                100% { transform: rotate(180deg); }
                            }
                            .d-none { display: none; }
                        </style>
                    </head>
                    <body>
                        <div id='page-loader' class='loader-container wait-for'>
                            <div class='wait-for hourglass'>⏳</div>
                            <div class='wait-for'>Rendering document components...</div>
                        </div>

                        [BODY_CONTENT]

                        <script>[SCRIPT_CONTENT]</script>
                    </body>
                    </html>";

                string finalHtml = secureOuterShell
                    .Replace("[BODY_CONTENT]", bodyContent)
                    .Replace("[SCRIPT_CONTENT]", scriptToInject);

                //Properties.Settings.Default.LastOpenedFile = filePath;
                //Properties.Settings.Default.Save();
                Program.Settings.LastOpened = filePath;
                Program.Settings.Save();

                lblStatus.Text = "Loading and rendering document components...";
                //Application.DoEvents();

                //webView21.CoreWebView2.NavigateToString(finalHtml);
                var tempFile = Path.Combine(Path.GetTempPath(), "mdglance.html");
                File.WriteAllText(tempFile, finalHtml, Encoding.UTF8);
                webView21.CoreWebView2.Navigate(new Uri(tempFile).AbsoluteUri);
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

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new About().ShowDialog();
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {

        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (!this.Visible) return;
            Program.Settings.SplitterPosition = splitContainer1.SplitterDistance;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Program.Settings.Save();
        }
    }
}