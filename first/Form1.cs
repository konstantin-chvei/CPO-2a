using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace first
{
    public partial class Form1 : Form
    {
        private RichTextBox _editor;
        private RichTextBox _details;
        private Label _absoluteValue;
        private Label _relativeValue;
        private Label _nestingValue;
        private Label _statusLabel;
        private Label _fileLabel;
        private string _currentFile;
        private bool _loadingText;
        private bool _runtimeInterfaceInitialized;

        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_runtimeInterfaceInitialized || IsInDesignMode())
                return;

            _runtimeInterfaceInitialized = true;
            BuildInterface();
            LoadExample();
            RunAnalysis();
        }

        private bool IsInDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime ||
                (Site != null && Site.DesignMode);
        }

        private void BuildInterface()
        {
            Text = "Анализатор Groovy";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1240, 790);
            MinimumSize = new Size(1020, 650);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(244, 247, 251);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Margin = Padding.Empty;
            root.Padding = Padding.Empty;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.FromArgb(25, 39, 62);
            root.Controls.Add(header, 0, 0);

            Label title = new Label();
            title.Text = "МЕТРИКА ДЖИЛБА";
            title.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.White;
            title.AutoSize = true;
            title.Location = new Point(22, 12);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Статический анализ ветвлений, относительной сложности и вложенности";
            subtitle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            subtitle.ForeColor = Color.FromArgb(195, 207, 223);
            subtitle.AutoSize = true;
            subtitle.Location = new Point(24, 48);
            header.Controls.Add(subtitle);

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(12, 7, 12, 5);
            toolbar.BackColor = Color.White;
            root.Controls.Add(toolbar, 0, 1);

            Button sampleButton = CreateButton("Загрузить пример", Color.FromArgb(235, 240, 247), Color.FromArgb(35, 50, 70));
            sampleButton.Width = 148;
            sampleButton.Click += delegate { LoadExample(); RunAnalysis(); };
            toolbar.Controls.Add(sampleButton);

            Button openButton = CreateButton("Открыть .groovy…", Color.FromArgb(235, 240, 247), Color.FromArgb(35, 50, 70));
            openButton.Width = 142;
            openButton.Click += OpenFile;
            toolbar.Controls.Add(openButton);

            Button saveButton = CreateButton("Сохранить", Color.FromArgb(235, 240, 247), Color.FromArgb(35, 50, 70));
            saveButton.Width = 108;
            saveButton.Click += SaveFile;
            toolbar.Controls.Add(saveButton);

            Button analyzeButton = CreateButton("Рассчитать метрики", Color.FromArgb(41, 111, 214), Color.White);
            analyzeButton.Width = 166;
            analyzeButton.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point);
            analyzeButton.Click += delegate { RunAnalysis(); };
            toolbar.Controls.Add(analyzeButton);
            AcceptButton = analyzeButton;

            _fileLabel = new Label();
            _fileLabel.AutoSize = true;
            _fileLabel.ForeColor = Color.FromArgb(102, 116, 136);
            _fileLabel.Margin = new Padding(12, 9, 3, 0);
            toolbar.Controls.Add(_fileLabel);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.Size = new Size(1200, 680);
            split.Panel1MinSize = 430;
            split.Panel2MinSize = 330;
            split.SplitterWidth = 6;
            split.SplitterDistance = 755;
            split.BackColor = Color.FromArgb(220, 226, 235);
            root.Controls.Add(split, 0, 2);

            BuildEditorPanel(split.Panel1);
            BuildResultsPanel(split.Panel2);

            _statusLabel = new Label();
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.Padding = new Padding(12, 4, 8, 2);
            _statusLabel.BackColor = Color.FromArgb(235, 240, 247);
            _statusLabel.ForeColor = Color.FromArgb(76, 91, 112);
            _statusLabel.Text = "Готово";
            root.Controls.Add(_statusLabel, 0, 3);
        }

        private void BuildEditorPanel(Control parent)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.White;
            layout.Padding = new Padding(12, 10, 8, 10);
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            parent.Controls.Add(layout);

            Label heading = new Label();
            heading.Text = "Исходный код Groovy";
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point);
            heading.ForeColor = Color.FromArgb(36, 50, 70);
            layout.Controls.Add(heading, 0, 0);

            _editor = new RichTextBox();
            _editor.Dock = DockStyle.Fill;
            _editor.AcceptsTab = true;
            _editor.WordWrap = false;
            _editor.DetectUrls = false;
            _editor.HideSelection = false;
            _editor.BorderStyle = BorderStyle.FixedSingle;
            _editor.BackColor = Color.FromArgb(251, 252, 254);
            _editor.ForeColor = Color.FromArgb(34, 45, 61);
            _editor.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            _editor.TextChanged += EditorTextChanged;
            layout.Controls.Add(_editor, 0, 1);
        }

        private void BuildResultsPanel(Control parent)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Color.FromArgb(244, 247, 251);
            layout.Padding = new Padding(10, 10, 12, 10);
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
            parent.Controls.Add(layout);

            Label heading = new Label();
            heading.Text = "Результаты";
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point);
            heading.ForeColor = Color.FromArgb(36, 50, 70);
            layout.Controls.Add(heading, 0, 0);

            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.Margin = new Padding(0, 2, 0, 8);
            cards.ColumnCount = 3;
            cards.RowCount = 1;
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel absoluteCard = CreateMetricCard("CL · абсолютная", Color.FromArgb(41, 111, 214), out _absoluteValue);
            Panel relativeCard = CreateMetricCard("cl · относительная", Color.FromArgb(23, 155, 116), out _relativeValue);
            Panel nestingCard = CreateMetricCard("CLI · вложенность", Color.FromArgb(225, 145, 42), out _nestingValue);
            cards.Controls.Add(absoluteCard, 0, 0);
            cards.Controls.Add(relativeCard, 1, 0);
            cards.Controls.Add(nestingCard, 2, 0);
            layout.Controls.Add(cards, 0, 1);

            _details = new RichTextBox();
            _details.Dock = DockStyle.Fill;
            _details.ReadOnly = true;
            _details.DetectUrls = false;
            _details.BorderStyle = BorderStyle.FixedSingle;
            _details.BackColor = Color.White;
            _details.ForeColor = Color.FromArgb(47, 59, 76);
            _details.Font = new Font("Segoe UI", 9.2F, FontStyle.Regular, GraphicsUnit.Point);
            _details.WordWrap = true;
            _details.HideSelection = true;
            _details.Text = "Нажмите «Рассчитать метрики».";
            layout.Controls.Add(_details, 0, 2);

        }

        private Panel CreateMetricCard(string caption, Color accent, out Label value)
        {
            Panel card = new Panel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(3, 2, 3, 4);
            card.BackColor = Color.White;
            card.Padding = new Padding(8, 8, 5, 5);

            Panel stripe = new Panel();
            stripe.Dock = DockStyle.Top;
            stripe.Height = 4;
            stripe.BackColor = accent;
            card.Controls.Add(stripe);

            Label captionLabel = new Label();
            captionLabel.Dock = DockStyle.Bottom;
            captionLabel.Height = 28;
            captionLabel.Text = caption;
            captionLabel.TextAlign = ContentAlignment.MiddleCenter;
            captionLabel.ForeColor = Color.FromArgb(95, 108, 127);
            captionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
            card.Controls.Add(captionLabel);

            value = new Label();
            value.Dock = DockStyle.Fill;
            value.Text = "—";
            value.TextAlign = ContentAlignment.MiddleCenter;
            value.ForeColor = accent;
            value.Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold, GraphicsUnit.Point);
            card.Controls.Add(value);
            return card;
        }

        private Button CreateButton(string text, Color backColor, Color foreColor)
        {
            Button button = new Button();
            button.Text = text;
            button.Height = 34;
            button.Margin = new Padding(3, 1, 5, 1);
            button.Padding = new Padding(8, 0, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private void LoadExample()
        {
            string[] candidates = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "examples", "GilbDemo.groovy"),
                Path.Combine(Directory.GetCurrentDirectory(), "first", "examples", "GilbDemo.groovy"),
                Path.Combine(Directory.GetCurrentDirectory(), "examples", "GilbDemo.groovy")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    try
                    {
                        string text = ReadTextFile(candidates[i]);
                        _currentFile = null;
                        SetEditorText(text);
                        SetFileLabel(Path.GetFileName(candidates[i]) + " · пример");
                        SetStatus("Загружен демонстрационный пример");
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Не удалось открыть пример.\r\n" + ex.Message,
                            "Ошибка чтения", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            _currentFile = null;
            SetEditorText(String.Empty);
            SetFileLabel("Новый код");
            SetStatus("Файл примера не найден — откройте .groovy или вставьте код в редактор");
        }

        private void OpenFile(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Открыть исходный код Groovy";
                dialog.Filter = "Файлы Groovy (*.groovy;*.gvy)|*.groovy;*.gvy|Все файлы (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    _currentFile = dialog.FileName;
                    SetEditorText(ReadTextFile(_currentFile));
                    SetFileLabel(Path.GetFileName(_currentFile));
                    SetStatus("Файл открыт");
                    RunAnalysis();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось прочитать файл.\r\n" + ex.Message,
                        "Ошибка чтения", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveFile(object sender, EventArgs e)
        {
            string path = _currentFile;
            if (String.IsNullOrEmpty(path) || !File.Exists(path))
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = "Сохранить исходный код Groovy";
                    dialog.Filter = "Файлы Groovy (*.groovy)|*.groovy|Все файлы (*.*)|*.*";
                    dialog.DefaultExt = "groovy";
                    dialog.AddExtension = true;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    path = dialog.FileName;
                }
            }

            try
            {
                File.WriteAllText(path, _editor.Text, new UTF8Encoding(false));
                _currentFile = path;
                SetFileLabel(Path.GetFileName(path));
                SetStatus("Файл сохранён");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить файл.\r\n" + ex.Message,
                    "Ошибка записи", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunAnalysis()
        {
            try
            {
                AnalysisResult result = GroovyAnalyzer.Analyze(_editor == null ? String.Empty : _editor.Text);
                _absoluteValue.Text = result.AbsoluteComplexity.ToString(CultureInfo.CurrentCulture);
                _relativeValue.Text = result.RelativeComplexity.ToString("0.000", CultureInfo.CurrentCulture);
                _nestingValue.Text = result.MaxNesting.ToString(CultureInfo.CurrentCulture);

                StringBuilder details = new StringBuilder();
                details.AppendLine("CL = " + result.AbsoluteComplexity + " ветвлений");
                if (result.TotalStatements > 0)
                {
                    details.AppendLine("cl = " + result.AbsoluteComplexity + " / " + result.TotalStatements + " = " +
                        result.RelativeComplexity.ToString("0.000", CultureInfo.CurrentCulture) +
                        " (" + (result.RelativeComplexity * 100.0).ToString("0.0", CultureInfo.CurrentCulture) + " %)");
                }
                else
                {
                    details.AppendLine("cl не вычислена: в исходном коде не найдено операторов (N = 0).");
                }
                details.AppendLine("CLI = " + result.MaxNesting + " (верхний уровень = 0)");
                details.AppendLine();
                details.AppendLine("Состав подсчёта:");
                details.AppendLine("  if / else-if: " + result.IfCount);
                details.AppendLine("  циклы for / while / do-while: " + result.LoopCount);
                details.AppendLine("  switch: " + result.SwitchCount + " оператор(а), case-меток без default: " + result.SwitchCaseCount);
                details.AppendLine("  тернарные ?: / Elvis-операторы: " + result.TernaryCount);
                details.AppendLine("  всего операторов N: " + result.TotalStatements);

                if (result.Warnings.Count > 0)
                {
                    details.AppendLine();
                    details.AppendLine("Замечания анализатора:");
                    for (int i = 0; i < result.Warnings.Count; i++)
                        details.AppendLine("  • " + result.Warnings[i]);
                }
                else
                {
                    details.AppendLine();
                    details.AppendLine("Лексических ошибок и незакрытых скобок не обнаружено.");
                }

                _details.Text = details.ToString();
                SetStatus("Расчёт завершён · N = " + result.TotalStatements +
                    " · замечаний: " + result.Warnings.Count);
            }
            catch (Exception ex)
            {
                SetStatus("Не удалось выполнить анализ");
                MessageBox.Show("Ошибка при анализе кода.\r\n" + ex.Message,
                    "Ошибка анализатора", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditorTextChanged(object sender, EventArgs e)
        {
            if (!_loadingText)
                SetStatus("Код изменён — нажмите «Рассчитать метрики» для обновления результата");
        }

        private void SetEditorText(string text)
        {
            _loadingText = true;
            _editor.Text = text ?? String.Empty;
            _editor.SelectionStart = 0;
            _editor.SelectionLength = 0;
            _loadingText = false;
        }

        private static string ReadTextFile(string path)
        {
            using (StreamReader reader = new StreamReader(path, true))
                return reader.ReadToEnd();
        }

        private void SetFileLabel(string text)
        {
            if (_fileLabel != null)
                _fileLabel.Text = text;
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
                _statusLabel.Text = text;
        }
    }
}
