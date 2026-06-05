/*
 * @brief About Form
 * 
 * @author Prahlad Yeri <prahladyeri@yahoo.com>
 * @license MIT
 * @date 2026-05-31
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace mdglance
{
    partial class About : Form
    {
        public About()
        {
            InitializeComponent();
            this.Text = String.Format("About {0}", AssemblyTitle);
            this.labelProductName.Text = AssemblyProduct;
            //this.labelVersion.Text = String.Format("Version {0}", AssemblyVersion);
            this.labelVersion.Text = String.Format("Version {0}", AssemblyVersion);
            this.labelCopyright.Text = AssemblyCopyright;
            this.textBoxDescription.Text = AssemblyDescription;

            //logoPictureBox.Image=  
            // Get the absolute physical file path of the currently running .exe
            string exePath = Assembly.GetExecutingAssembly().Location;

            // Extract the primary icon asset directly out of the binary's win32 resource table
            logoPictureBox.Image = Icon.ExtractAssociatedIcon(exePath).ToBitmap();

            PopulateThirdPartyLicenses();
        }

        private void PopulateThirdPartyLicenses()
        {

            // 1. Mock/Pre-populated structure matching your array of dicts setup
            var _components = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Name", "MDGlance" }, { "License", "MIT" }, { "Path", "licenses/mdglance.txt" } },
                new Dictionary<string, string> { { "Name", "Markdig" }, { "License", "BSD-2-Clause" }, { "Path", "licenses/markdig.txt" } },
                new Dictionary<string, string> { { "Name", "Tango Icons" }, { "License", "Public Domain" }, { "Path", "licenses/tango-icons.txt" } },
                new Dictionary<string, string> { { "Name", "Fugue Icons" }, { "License", "CC-BY-3.0" }, { "Path", "licenses/fugue-icons.txt" } }
            };

            this.labelLicense.Text = _components[0]["License"] + " License";
            this.labelLicense.Tag = _components[0]["Path"];
            this.labelLicense.LinkClicked += LicenseLink_Clicked;

            // 2. Instantiate a FlowLayoutPanel container to host the buttons inline
            FlowLayoutPanel linkContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Margin = new Padding(0)
            };

            Label lbl = new Label
            {
                Text = "Credits:",
                AutoSize = true,
                
            };
            linkContainer.Controls.Add(lbl);

            // 3. Loop through your definitions to build out the LinkLabels
            foreach (var lib in _components)
            {
                if (lib["Name"] == Application.ProductName) continue;
                LinkLabel btnLink = new LinkLabel
                {
                    Text = $"{lib["Name"]} ({lib["License"]})",
                    AutoSize = true,
                    Margin = new Padding(0, 0, 10, 5), // Tight padding spacing for a clean UI layout
                    Tag = lib["Path"] // Store the relative license path payload directly inside the control's Tag
                };

                // Bind the interaction click handler
                btnLink.LinkClicked += LicenseLink_Clicked;

                // Append the button link into our dynamic container flow
                linkContainer.Controls.Add(btnLink);
            }

            // 4. Inject the layout container directly into your target cell coordinates
            // Column Index: 1 (0-indexed base for Col 2), Row Index: 5 (0-indexed base for Row 6)
            tableLayoutPanel.Controls.Add(linkContainer, 1, 5);
        }

        private void LicenseLink_Clicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Safely extract the link control instance
            if (sender is LinkLabel clickedLink && clickedLink.Tag is string relativePath)
            {
                try
                {
                    // Map the path cleanly relative to the application's root directory base
                    string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);

                    if (File.Exists(fullPath))
                    {
                        // Instantiate a clean process call to native notepad.exe
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = "notepad.exe",
                            Arguments = $"\"{fullPath}\"", // Encapsulate inside quotes to safely handle paths containing spaces
                            UseShellExecute = true
                        };

                        Process.Start(startInfo);
                    }
                    else
                    {
                        MessageBox.Show($"Could not locate license documentation at:\n{relativePath}",
                                        "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening system text editor: {ex.Message}",
                                    "Execution Fault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }


        #region Assembly Attribute Accessors

        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{v.Major}.{v.Minor}";
                //return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
        #endregion
    }
}
