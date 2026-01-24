using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using Seagull.BarTender.Print;

namespace smart_label
{
    public partial class Form1 : Form
    {
        private const string IniFileName = "config.ini";
        private string templatesFolderPath = string.Empty;
        private string selectedTemplatePath = string.Empty;
        private Dictionary<int, string> dataSourceNames = new Dictionary<int, string>();
        private Dictionary<int, string> dataSourceFields = new Dictionary<int, string>();
        private int previousCount = 0;
        private bool suppressCountPrompt = false;
        // indicates a print operation is in progress
        private volatile bool isPrinting = false;

        // 日志级别枚举
        private enum LogLevel
        {
            Info,
            Success,
            Warning,
            Error
        }

        // helper item for combo box
        private class TemplateItem
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public TemplateItem(string name, string fullPath)
            {
                Name = name;
                FullPath = fullPath;
            }
            public override string ToString() => Name;
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Determine config path
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IniFileName);
            if (File.Exists(path))
            {
                LoadConfig(path);
            }
            else
            {
                // create default inputs
                CreateInputControls((int)numericUpDownCount.Value);
                // default templates folder under application directory: \templates
                templatesFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
                try
                {
                    if (!Directory.Exists(templatesFolderPath)) Directory.CreateDirectory(templatesFolderPath);
                }
                catch { }
                PopulateTemplateList(templatesFolderPath);
            }
            previousCount = (int)numericUpDownCount.Value;
            SetStatus("就绪");
            AddLog("系统启动完成", LogLevel.Info);
        }

        private void numericUpDownCount_ValueChanged(object sender, EventArgs e)
        {
            if (suppressCountPrompt) return;
             int newCount = (int)numericUpDownCount.Value;
             if (newCount > previousCount)
             {
                 // prompt user to provide display name and field mapping for each newly added data source
                 for (int i = previousCount + 1; i <= newCount; i++)
                 {
                     var cfg = PromptForDataSourceConfig(i);
                     if (cfg == null)
                     {
                         // user cancelled: revert to previousCount
                         numericUpDownCount.Value = previousCount;
                         return;
                     }
                     if (string.IsNullOrWhiteSpace(cfg.Name)) cfg.Name = $"数据源 {i}";
                     if (string.IsNullOrWhiteSpace(cfg.Field)) cfg.Field = "DS" + i;
                     dataSourceNames[i] = cfg.Name;
                     dataSourceFields[i] = cfg.Field;
                 }
                 AddLog($"数据源数量从 {previousCount} 增加到 {newCount}", LogLevel.Info);
             }
             else if (newCount < previousCount)
             {
                 // remove names beyond newCount
                 for (int i = newCount + 1; i <= previousCount; i++)
                 {
                     if (dataSourceNames.ContainsKey(i)) dataSourceNames.Remove(i);
                     if (dataSourceFields.ContainsKey(i)) dataSourceFields.Remove(i);
                 }
                 AddLog($"数据源数量从 {previousCount} 减少到 {newCount}", LogLevel.Info);
             }

             CreateInputControls(newCount);
             previousCount = newCount;
         }

        // Data source configuration with Display Name and Field mapping
        private class DataSourceConfig
        {
            public string Name { get; set; }
            public string Field { get; set; }
        }

        // Prompt user to input data source display name and mapped field. Returns config or null if cancelled.
        private DataSourceConfig PromptForDataSourceConfig(int index)
        {
            using (var f = new Form())
            {
                f.Text = "配置数据源";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(420, 160);
                f.MinimizeBox = false;
                f.MaximizeBox = false;

                var lblName = new Label() { Left = 10, Top = 10, Width = 390, Text = $"第 {index} 个数据源 显示名称：" };
                var tbName = new TextBox() { Left = 10, Top = 30, Width = 390 };

                var lblField = new Label() { Left = 10, Top = 60, Width = 390, Text = "映射到模板的字段名（例如 DS1 或 FieldName）：" };
                var tbField = new TextBox() { Left = 10, Top = 80, Width = 390 };

                var btnOk = new Button() { Text = "确定", Left = 240, Width = 80, Top = 110, DialogResult = DialogResult.OK };
                var btnCancel = new Button() { Text = "取消", Left = 330, Width = 80, Top = 110, DialogResult = DialogResult.Cancel };

                f.Controls.Add(lblName);
                f.Controls.Add(tbName);
                f.Controls.Add(lblField);
                f.Controls.Add(tbField);
                f.Controls.Add(btnOk);
                f.Controls.Add(btnCancel);

                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;

                var dr = f.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    return new DataSourceConfig { Name = tbName.Text ?? string.Empty, Field = tbField.Text ?? string.Empty };
                }
                return null;
            }
        }

        private void CreateInputControls(int count)
        {
            // 1. 基础校验，避免空引用和非法参数
            if (panelInputs == null)
            {
                MessageBox.Show("面板控件未初始化！");
                return;
            }
            if (count < 0 || count > 100) // 限制最大创建数量，可根据实际调整
            {
                MessageBox.Show("控件数量必须在0-100之间！");
                return;
            }

            // 2. 彻底清理旧控件（包括移除事件绑定，避免内存泄漏）
            ClearOldControls();

            // 3. 批量创建控件，减少布局计算次数
            panelInputs.SuspendLayout();
            int top = 10;
            int labelWidth = 80; // 固定Label宽度，避免重叠
            int textBoxMinWidth = 100;

            for (int i = 1; i <= count; i++)
            {
                // 创建Label并优化布局
                var lbl = new Label
                {
                    Text = (dataSourceNames?.ContainsKey(i) == true ? dataSourceNames[i] : $"数据源 {i}") + ":",
                    AutoSize = false, // 关闭自动大小，固定宽度
                    Size = new Size(labelWidth, 21), // 与TextBox高度一致
                    Location = new Point(10, top),
                    TextAlign = ContentAlignment.MiddleRight, // 文字右对齐，更美观
                    Anchor = AnchorStyles.Top | AnchorStyles.Left // 跟随面板左边缘
                };

                // 创建TextBox并优化布局
                var txt = new TextBox
                {
                    Name = GetTextBoxName(i),
                    Location = new Point(lbl.Right + 5, top), // 基于Label位置计算，避免硬编码
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Height = 21 // 固定高度，统一样式
                };
                // 动态计算TextBox宽度（兼容面板大小变化）
                int textBoxWidth = Math.Max(textBoxMinWidth, panelInputs.ClientSize.Width - txt.Left - 10);
                txt.Size = new Size(textBoxWidth, txt.Height);

                // 绑定事件（确保每次只绑定一次）
                txt.KeyDown -= DataSourceTextBox_KeyDown; // 先移除再添加，避免重复绑定
                txt.KeyDown += DataSourceTextBox_KeyDown;

                // 添加到面板
                panelInputs.Controls.Add(lbl);
                panelInputs.Controls.Add(txt);

                // 动态调整间距（基于控件高度，适配不同DPI）
                top += txt.Height + 14; // 控件高度+间距，比固定35更灵活
            }

            panelInputs.ResumeLayout(true); // true表示立即执行布局更新
        }

        /// <summary>
        /// 彻底清理旧控件，移除事件绑定并释放资源
        /// </summary>
        private void ClearOldControls()
        {
            if (panelInputs?.Controls == null) return;

            // 遍历所有控件，移除事件绑定
            foreach (Control ctrl in panelInputs.Controls)
            {
                if (ctrl is TextBox txt)
                {
                    txt.KeyDown -= DataSourceTextBox_KeyDown; // 移除事件绑定
                }
            }
            // 清除所有控件
            panelInputs.Controls.Clear();
        }

        private void DataSourceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // 消除回车的提示音

                // block any input focus changes while a print is in progress
                if (isPrinting) return;

                var tb = sender as TextBox;
                if (tb == null) return;
                // Expect name format txtDataSource_N
                var parts = tb.Name.Split('_');
                if (parts.Length < 2) return;
                if (!int.TryParse(parts[1], out int idx)) return;
                int next = idx + 1;
                var nextCtrl = panelInputs.Controls.Find(GetTextBoxName(next), true).FirstOrDefault() as TextBox;
                if (nextCtrl != null)
                {
                    nextCtrl.Focus();
                    nextCtrl.SelectAll();
                }
                else
                {
                    // last textbox: start print synchronously
                    if (isPrinting) return;

                    if (string.IsNullOrWhiteSpace(selectedTemplatePath) || !File.Exists(selectedTemplatePath))
                    {
                        MessageBox.Show("请先选择有效的模板文件 (.btw)");
                        return;
                    }

                    try
                    {
                        //isPrinting = true;
                        //SetInputsReadOnly(true);
                        //btnPrintTemplate.Enabled = false;
                        SetStatus("打印中...");
                        AddLog("开始打印操作", LogLevel.Info);
                        // ensure UI updates before blocking call
                        //Application.DoEvents();

                        // call synchronous print directly
                        PrintTemplate(selectedTemplatePath);

                        //System.Threading.Thread.Sleep(1000);
                        SetStatus("打印完成");
                        AddLog("打印完成", LogLevel.Success);
                        ClearInputsAndFocusFirst();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("打印失败: " + ex.Message);
                        SetStatus("打印失败");
                        AddLog($"打印失败: {ex.Message}", LogLevel.Error);
                    }
                    finally
                    {
                        isPrinting = false;
                        SetInputsReadOnly(false);
                        btnPrintTemplate.Enabled = true;
                    }
                }
            }
        }

        private void btnPrintTemplate_Click(object sender, EventArgs e)
        {
            if (isPrinting) return;

            if (string.IsNullOrWhiteSpace(selectedTemplatePath) || !File.Exists(selectedTemplatePath))
            {
                MessageBox.Show("请先选择有效的模板文件 (.btw)");
                return;
            }

            try
            {
                isPrinting = true;
                SetInputsReadOnly(true);
                btnPrintTemplate.Enabled = false;
                SetStatus("打印中...");
                AddLog("开始打印操作", LogLevel.Info);
                //Application.DoEvents();

                // synchronous print
                PrintTemplate(selectedTemplatePath);

                SetStatus("打印完成");
                AddLog("打印完成", LogLevel.Success);
                ClearInputsAndFocusFirst();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打印失败: " + ex.Message);
                SetStatus("打印失败");
                AddLog($"打印失败: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                SetInputsReadOnly(false);
                btnPrintTemplate.Enabled = true;
                isPrinting = false;
            }
        }

        private void PrintTemplate(string templatePath)
        {
            Engine btEngine = null;
            try
            {
                btEngine = new Engine(true);
                LabelFormatDocument doc = btEngine.Documents.Open(templatePath);

                // collect missing fields
                var missingFields = new List<string>();

                // set named substrings DS1, DS2, ... from inputs
                for (int i = 1; i <= (int)numericUpDownCount.Value; i++)
                {
                    var tb = panelInputs.Controls.Find(GetTextBoxName(i), true).FirstOrDefault() as TextBox;
                    if (tb == null) continue;
                    string value = tb.Text ?? string.Empty;

                    // prefer configured field mapping if exists, else fall back to DS{i}
                    string field = dataSourceFields.ContainsKey(i) && !string.IsNullOrWhiteSpace(dataSourceFields[i]) ? dataSourceFields[i] : ("DS" + i);

                    try
                    {
                        // access named substring; will throw or be null if not present
                        var subStrings = doc.SubStrings;
                        var named = subStrings[field];
                        if (named != null)
                        {
                            named.Value = value;
                        }
                        else
                        {
                            missingFields.Add(field);
                        }
                    }
                    catch
                    {
                        // record missing and continue
                        missingFields.Add(field);
                    }
                }

                if (missingFields.Count > 0)
                {
                    // throw with a clear message so the async wrapper can show it in UI thread
                    var unique = string.Join(", ", missingFields.Distinct());
                    throw new InvalidOperationException("模板中未找到以下字段: " + unique);
                }

                try
                {
                    doc.Print("BarPrint" + DateTime.Now, 1);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("BarTender PrintOut failed: " + ex.Message);
                    throw new InvalidOperationException("BarTender PrintOut failed: " + ex.Message, ex);
                }

                // Close via reflection to avoid compile-time enum dependency
                try
                {
                    var closeMethod = ((object)doc).GetType().GetMethod("Close");
                    if (closeMethod != null)
                    {
                        var parms = closeMethod.GetParameters();
                        if (parms != null && parms.Length == 1)
                        {
                            var enumType = parms[0].ParameterType;
                            var dontSaveValue = Enum.ToObject(enumType, 0);
                            closeMethod.Invoke(((object)doc), new object[] { dontSaveValue });
                        }
                        else
                        {
                            closeMethod.Invoke(((object)doc), null);
                        }
                    }
                }
                catch { }
            }
            finally
            {
                if (btEngine != null)
                {
                    try { btEngine.Stop(); } catch { }
                }
            }
        }

        // Update the status label safely from any thread
        private void SetStatus(string text)
        {
            if (lblStatus == null) return;
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke((Action)(() => lblStatus.Text = text));
            }
            else
            {
                lblStatus.Text = text;
            }
        }

        // Simple INI helpers using WinAPI
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        private void IniWriteValue(string section, string key, string value, string filePath)
        {
            WritePrivateProfileString(section, key, value, filePath);
        }

        private string IniReadValue(string section, string key, string filePath)
        {
            StringBuilder sb = new StringBuilder(2048);
            GetPrivateProfileString(section, key, string.Empty, sb, sb.Capacity, filePath);
            return sb.ToString();
        }

        private string GetTextBoxName(int index) => $"txtDataSource_{index}";

        private void SetInputsReadOnly(bool readOnly)
        {
            if (panelInputs.InvokeRequired)
            {
                panelInputs.Invoke((Action)(() => SetInputsReadOnly(readOnly)));
                return;
            }

            foreach (Control c in panelInputs.Controls)
            {
                if (c is TextBox tb)
                {
                    tb.ReadOnly = readOnly;
                    tb.BackColor = readOnly ? SystemColors.Control : SystemColors.Window;
                    //tb.Enabled = !readOnly;
                }
            }
        }

        private void ClearInputsAndFocusFirst()
        {
            var first = panelInputs.Controls.Find(GetTextBoxName(1), true).FirstOrDefault() as TextBox;
            foreach (Control c in panelInputs.Controls)
            {
                if (c is TextBox tb && tb.Name.StartsWith("txtDataSource_"))
                {
                    tb.Text = string.Empty;
                }
            }
            if (first != null && !isPrinting)
            {
                first.Focus();
            }
        }

        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            // require password before saving
            const string requiredPassword = "20251129";
            string entered = PromptForPassword();
            if (entered == null)
            {
                // user cancelled
                return;
            }
            if (entered != requiredPassword)
            {
                MessageBox.Show("密码错误，配置未保存");
                return;
            }

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IniFileName);
            SaveConfig(path);
            AddLog($"配置已保存到: {path}", LogLevel.Success);
            MessageBox.Show("配置已保存: " + path);
        }

        private void btnLoadConfig_Click(object sender, EventArgs e)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IniFileName);
            if (File.Exists(path))
            {
                LoadConfig(path);
                AddLog($"配置已从 {path} 加载", LogLevel.Success);
                MessageBox.Show("配置已加载");
            }
            else
            {
                AddLog($"未找到配置文件: {path}", LogLevel.Warning);
                MessageBox.Show("未找到配置文件: " + path);
            }
        }

        // Shows a simple modal password prompt. Returns entered password or null if cancelled.
        private string PromptForPassword()
        {
            using (var f = new Form())
            {
                f.Text = "请输入保存密码";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ClientSize = new Size(320, 110);
                f.MinimizeBox = false;
                f.MaximizeBox = false;

                var lbl = new Label() { Left = 10, Top = 10, Width = 300, Text = "请输入密码以保存配置：" };
                var tb = new TextBox() { Left = 10, Top = 35, Width = 300, UseSystemPasswordChar = true };
                var btnOk = new Button() { Text = "确定", Left = 150, Width = 75, Top = 70, DialogResult = DialogResult.OK };
                var btnCancel = new Button() { Text = "取消", Left = 235, Width = 75, Top = 70, DialogResult = DialogResult.Cancel };

                f.Controls.Add(lbl);
                f.Controls.Add(tb);
                f.Controls.Add(btnOk);
                f.Controls.Add(btnCancel);

                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;

                var dr = f.ShowDialog(this);
                if (dr == DialogResult.OK)
                {
                    return tb.Text ?? string.Empty;
                }
                return null;
            }
        }

        private void SaveConfig(string path)
        {
            // Write count and each data source value
            IniWriteValue("General", "Count", numericUpDownCount.Value.ToString(), path);
            IniWriteValue("General", "TemplatesFolder", templatesFolderPath ?? string.Empty, path);
            for (int i = 1; i <= (int)numericUpDownCount.Value; i++)
            {
                 var tb = panelInputs.Controls.Find(GetTextBoxName(i), true).FirstOrDefault() as TextBox;
                 string val = tb?.Text ?? string.Empty;
                 IniWriteValue("DataSource" + i, "Value", val, path);
                string name = dataSourceNames.ContainsKey(i) ? dataSourceNames[i] : string.Empty;
                IniWriteValue("DataSource" + i, "Name", name, path);
                string field = dataSourceFields.ContainsKey(i) ? dataSourceFields[i] : string.Empty;
                IniWriteValue("DataSource" + i, "Field", field, path);
             }
        }

        private void LoadConfig(string path)
        {
            string countStr = IniReadValue("General", "Count", path);
            int count = 1;
            if (!int.TryParse(countStr, out count) || count < 0) count = 0;
            if (count > numericUpDownCount.Maximum) count = (int)numericUpDownCount.Maximum;

            // load names and field mappings for data sources first
            dataSourceNames.Clear();
            dataSourceFields.Clear();
            for (int i = 1; i <= count; i++)
            {
                string name = IniReadValue("DataSource" + i, "Name", path);
                if (!string.IsNullOrWhiteSpace(name)) dataSourceNames[i] = name;
                string field = IniReadValue("DataSource" + i, "Field", path);
                if (!string.IsNullOrWhiteSpace(field)) dataSourceFields[i] = field;
            }

            // set count without triggering prompt
            suppressCountPrompt = true;
            try
            {
                numericUpDownCount.Value = count;
            }
            finally
            {
                suppressCountPrompt = false;
            }

            templatesFolderPath = IniReadValue("General", "TemplatesFolder", path);
            if (string.IsNullOrWhiteSpace(templatesFolderPath))
            {
                templatesFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
                try { if (!Directory.Exists(templatesFolderPath)) Directory.CreateDirectory(templatesFolderPath); } catch { }
            }
            if (!string.IsNullOrWhiteSpace(templatesFolderPath) && Directory.Exists(templatesFolderPath))
            {
                PopulateTemplateList(templatesFolderPath);
            }

            CreateInputControls(count);
            for (int i = 1; i <= count; i++)
            {
                string val = IniReadValue("DataSource" + i, "Value", path);
                var tb = panelInputs.Controls.Find(GetTextBoxName(i), true).FirstOrDefault() as TextBox;
                if (tb != null) tb.Text = val;
            }
            previousCount = count;
        }

        // Populate the combo box with .btw templates from the specified folder
        private void PopulateTemplateList(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                comboBoxTemplates.Items.Clear();
                lblSelectedTemplate.Text = "未找到 .btw 模板";
                selectedTemplatePath = string.Empty;
                return;
            }

            comboBoxTemplates.Items.Clear();
            try
            {
                var files = Directory.GetFiles(folder, "*.btw");
                foreach (var f in files)
                {
                    comboBoxTemplates.Items.Add(new TemplateItem(Path.GetFileName(f), f));
                }
                if (files.Length == 0)
                {
                    lblSelectedTemplate.Text = "未找到 .btw 模板";
                    selectedTemplatePath = string.Empty;
                }
                else
                {
                    comboBoxTemplates.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取模板文件夹出错: " + ex.Message);
            }
        }

        private void comboBoxTemplates_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = comboBoxTemplates.SelectedItem as TemplateItem;
            if (item != null)
            {
                selectedTemplatePath = item.FullPath;
                lblSelectedTemplate.Text = item.Name;
                LoadTemplatePreview(selectedTemplatePath);
                AddLog($"已选择模板: {item.Name}", LogLevel.Info);
            }
            else
            {
                selectedTemplatePath = string.Empty;
                lblSelectedTemplate.Text = "未选择模板文件";
                pictureBoxPreview.Image = null;
            }
        }

        private void LoadTemplatePreview(string templatePath)
        {
            pictureBoxPreview.Image = null; // clear existing image
            SetStatus("生成预览...");
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                SetStatus("就绪");
                return;
            }

            string tempFile = null;
            Engine btEngine = null;
            try
            {
                btEngine = new Engine(true);
                LabelFormatDocument doc = btEngine.Documents.Open(templatePath);

                try
                {
                    // create temp path with .png extension
                    tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
                    try
                    {
                        doc.ExportImageToFile(tempFile, ImageType.PNG, Seagull.BarTender.Print.ColorDepth.ColorDepth256, new Resolution(300, 300), OverwriteOptions.Overwrite);
                    }
                    catch
                    {
                        // fallback to BMP if PNG not supported
                        string tmpBmp = Path.ChangeExtension(tempFile, ".bmp");
                        doc.ExportImageToFile(tmpBmp, ImageType.BMP, Seagull.BarTender.Print.ColorDepth.ColorDepth256, new Resolution(300, 300), OverwriteOptions.Overwrite);
                        if (File.Exists(tmpBmp))
                        {
                            // move bmp to our tempFile path so we can delete consistently
                            File.Move(tmpBmp, tempFile);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(tempFile) && File.Exists(tempFile))
                    {
                        // load into memory then delete file
                        using (var img = Image.FromFile(tempFile))
                        {
                            var bmp = new Bitmap(img);
                            var old = pictureBoxPreview.Image;
                            pictureBoxPreview.Image = bmp;
                            old?.Dispose();
                        }
                    }
                }
                catch
                {
                    // ignore preview generation errors
                }

                // Close via reflection to avoid compile-time enum dependency
                try
                {
                    var closeMethod = ((object)doc).GetType().GetMethod("Close");
                    if (closeMethod != null)
                    {
                        var parms = closeMethod.GetParameters();
                        if (parms != null && parms.Length == 1)
                        {
                            var enumType = parms[0].ParameterType;
                            var dontSaveValue = Enum.ToObject(enumType, 0);
                            closeMethod.Invoke(((object)doc), new object[] { dontSaveValue });
                        }
                        else
                        {
                            closeMethod.Invoke(((object)doc), null);
                        }
                    }
                }
                catch { }
            }
            finally
            {
                // cleanup temp file
                try
                {
                    if (!string.IsNullOrWhiteSpace(tempFile) && File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch { }

                if (btEngine != null)
                {
                    try { btEngine.Stop(); } catch { }
                }
                SetStatus("就绪");
            }
        }

        /// <summary>
        /// 添加日志信息到日志显示框
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="level">日志级别</param>
        private void AddLog(string message, LogLevel level = LogLevel.Info)
        {
            if (textBoxLog == null) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string levelText = GetLogLevelText(level);
            string logLine = $"[{timestamp}] [{levelText}] {message}";

            if (textBoxLog.InvokeRequired)
            {
                textBoxLog.Invoke((Action)(() => AddLog(message, level)));
                return;
            }

            textBoxLog.AppendText(logLine + Environment.NewLine);
            textBoxLog.ScrollToCaret();

            // 限制日志行数,最多保留1000行
            if (textBoxLog.Lines.Length > 1000)
            {
                string[] lines = textBoxLog.Lines;
                string[] newLines = new string[1000];
                Array.Copy(lines, lines.Length - 1000, newLines, 0, 1000);
                textBoxLog.Lines = newLines;
            }
        }

        /// <summary>
        /// 获取日志级别文本
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <returns>日志级别文本</returns>
        private string GetLogLevelText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Success:
                    return "成功";
                case LogLevel.Warning:
                    return "警告";
                case LogLevel.Error:
                    return "错误";
                case LogLevel.Info:
                default:
                    return "信息";
            }
        }

        /// <summary>
        /// 清空日志按钮点击事件
        /// </summary>
        private void btnClearLog_Click(object sender, EventArgs e)
        {
            if (textBoxLog.InvokeRequired)
            {
                textBoxLog.Invoke((Action)(() => btnClearLog_Click(sender, e)));
                return;
            }

            textBoxLog.Clear();
            AddLog("日志已清空", LogLevel.Info);
        }

        /// <summary>
        /// 导出日志按钮点击事件
        /// </summary>
        private void btnExportLog_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxLog.Text))
            {
                MessageBox.Show("日志为空,无需导出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "文本文件 (*.txt)|*.txt|日志文件 (*.log)|*.log|所有文件 (*.*)|*.*";
                    sfd.DefaultExt = "log";
                    sfd.FileName = $"smart_label_log_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                    sfd.Title = "导出日志";

                    if (sfd.ShowDialog(this) == DialogResult.OK)
                    {
                        File.WriteAllText(sfd.FileName, textBoxLog.Text, Encoding.UTF8);
                        AddLog($"日志已导出到: {sfd.FileName}", LogLevel.Success);
                        MessageBox.Show($"日志已成功导出到:\n{sfd.FileName}", "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"导出日志失败: {ex.Message}", LogLevel.Error);
                MessageBox.Show($"导出日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
