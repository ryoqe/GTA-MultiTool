using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

public class ToggleSwitch : Control
{
    private bool _checked;
    public event EventHandler CheckedChanged;

    public bool Checked
    {
        get { return _checked; }
        set
        {
            if (_checked != value)
            {
                _checked = value;
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public ToggleSwitch()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.Size = new Size(200, 24);
        this.Cursor = Cursors.Hand;
        this.ForeColor = Color.White;
        this.Font = new Font("Segoe UI", 9, FontStyle.Regular);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Checked = !Checked;
        }
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color pColor = this.Parent != null ? this.Parent.BackColor : Color.FromArgb(20, 20, 20);
        using (Brush backBrush = new SolidBrush(pColor))
        {
            g.FillRectangle(backBrush, this.ClientRectangle);
        }

        int swWidth = 40;
        int swHeight = 20;
        int swY = (this.Height - swHeight) / 2;

        Color bgColor = Checked ? Color.FromArgb(252, 165, 36) : Color.FromArgb(60, 60, 60);
        using (Brush bgBrush = new SolidBrush(bgColor))
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, swY, swHeight, swHeight, 90, 180);
            path.AddArc(swWidth - swHeight, swY, swHeight, swHeight, 270, 180);
            path.CloseFigure();
            g.FillPath(bgBrush, path);
        }

        int thumbSize = swHeight - 6;
        int thumbX = Checked ? (swWidth - thumbSize - 3) : 3;
        int thumbY = swY + 3;
        using (Brush thumbBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(thumbBrush, thumbX, thumbY, thumbSize, thumbSize);
        }

        if (!string.IsNullOrEmpty(this.Text))
        {
            using (Brush textBrush = new SolidBrush(this.ForeColor))
            {
                StringFormat sf = new StringFormat();
                sf.LineAlignment = StringAlignment.Center;
                sf.Alignment = StringAlignment.Near;
                Rectangle textRect = new Rectangle(swWidth + 8, 0, this.Width - swWidth - 8, this.Height);
                g.DrawString(this.Text, this.Font, textBrush, textRect, sf);
            }
        }
    }
}

public class ModernButton : Button
{
    private int _borderRadius = 6;
    private bool _isHovered = false;
    private bool _isPressed = false;

    public int BorderRadius
    {
        get { return _borderRadius; }
        set { _borderRadius = value; Invalidate(); }
    }

    public ModernButton()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.FlatStyle = FlatStyle.Flat;
        this.FlatAppearance.BorderSize = 0;
        this.Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _isPressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        Graphics g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (this.Parent != null)
        {
            using (Brush parentBg = new SolidBrush(this.Parent.BackColor))
            {
                g.FillRectangle(parentBg, this.ClientRectangle);
            }
        }

        Color normalColor = this.BackColor;
        Color drawBg = normalColor;
        if (!this.Enabled)
        {
            drawBg = Color.FromArgb(40, 40, 40);
        }
        else if (_isPressed)
        {
            drawBg = Color.FromArgb(Math.Max(0, normalColor.R - 25), Math.Max(0, normalColor.G - 25), Math.Max(0, normalColor.B - 25));
        }
        else if (_isHovered)
        {
            drawBg = Color.FromArgb(Math.Min(255, normalColor.R + 25), Math.Min(255, normalColor.G + 25), Math.Min(255, normalColor.B + 25));
        }

        using (Brush brush = new SolidBrush(drawBg))
        {
            GraphicsPath path = GetRoundedRectPath(this.ClientRectangle, _borderRadius);
            g.FillPath(brush, path);
        }

        Color textColor = this.Enabled ? this.ForeColor : Color.Gray;
        using (Brush textBrush = new SolidBrush(textColor))
        {
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            g.DrawString(this.Text, this.Font, textBrush, this.ClientRectangle, sf);
        }
    }

    private GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int diameter = radius * 2;
        if (diameter > rect.Width) diameter = rect.Width;
        if (diameter > rect.Height) diameter = rect.Height;

        Rectangle arcRect = new Rectangle(rect.X, rect.Y, diameter, diameter);
        path.AddArc(arcRect, 180, 90);

        arcRect.X = rect.Right - diameter;
        path.AddArc(arcRect, 270, 90);

        arcRect.Y = rect.Bottom - diameter;
        path.AddArc(arcRect, 0, 90);

        arcRect.X = rect.X;
        path.AddArc(arcRect, 90, 90);

        path.CloseFigure();
        return path;
    }
}

public class BorderlessTabControl : TabControl
{
    public BorderlessTabControl()
    {
        this.Appearance = TabAppearance.FlatButtons;
        this.ItemSize = new Size(0, 1);
        this.SizeMode = TabSizeMode.Fixed;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x1328 && !DesignMode)
        {
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }
}
