using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using System.Threading;
using FrmTmr = System.Windows.Forms.Timer;

class Form1 : Form
{
    [DllImport("user32.dll")]
    public static extern int GetKeyboardState(byte[] keystate);
    byte[] keys = new byte[256];

    //Constants
    const byte SPD_PLAYER = 16;
    const byte SPD_BALL = 8;
    const byte SPD_GAME = 50;

    //Timers
    FrmTmr tm_draw, tm_key;
    Thread thd_rec, thd_snd;
    
    //Game objects
    Rectangle[] rc_obj;
    Graphics gfx;
    byte plid;

    //Ball
    Random rnd;
    sbyte mdx, mdy;
    
    //Networking
    TcpClient client;
    TcpListener server;
    Socket sock;

    public Form1()
    {
        //Form
        Text = "Pong Network";
        BackColor = Color.Black;
        BackgroundImage = new Bitmap(640, 480);
        BackgroundImageLayout = ImageLayout.Zoom;
        FormBorderStyle = FormBorderStyle.None;
        Bounds = Screen.PrimaryScreen.Bounds;
        MaximizeBox = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
        StartPosition = FormStartPosition.CenterScreen;
        gfx = Graphics.FromImage(BackgroundImage);
        //Objects
        rc_obj = new Rectangle[6];
        rc_obj[0] = new Rectangle(256, 12, 128, 16);
        rc_obj[1] = new Rectangle(256, 452, 128, 16);
        rc_obj[2] = new Rectangle(312, 232, 8, 8);
        new Form2(this).ShowDialog();
    }

    void tm_key_Tick(object sender, EventArgs e)
    {
        GetKeyboardState(keys);
        if ((keys[(int)Keys.Left] & 128) > 0 && rc_obj[plid].X > 0)
            rc_obj[plid].X -= SPD_PLAYER;
        else if ((keys[(int)Keys.Right] & 128) > 0 && rc_obj[plid].Right < 640)
            rc_obj[plid].X += SPD_PLAYER;
    }

    void tm_draw_Tick(object sender, EventArgs e)
    {
        DrawElements();
    }

    void tm_draw_Tick2(object sender, EventArgs e)
    {
        DrawElements();
        rc_obj[2].X += mdx;
        rc_obj[2].Y += mdy;
        if (rc_obj[2].IntersectsWith(rc_obj[0]) ||
            rc_obj[2].IntersectsWith(rc_obj[1]))
            mdy = (sbyte)-mdy;
        else if (rc_obj[2].X <= 0 || rc_obj[2].Right >= 640)
            mdx = (sbyte)-mdx;
        else if (rc_obj[2].Y < 0 || rc_obj[2].Bottom > 480)
            ResetRound();
    }

    void DrawElements()
    {
        gfx.FillRectangle(Brushes.Black, ClientRectangle);
        gfx.DrawImage(pong_net.Properties.Resources.bt, rc_obj[0]);
        gfx.DrawImage(pong_net.Properties.Resources.bt, rc_obj[1]);
        gfx.DrawImage(pong_net.Properties.Resources.bl, rc_obj[2]);
        Invalidate();
    }

    void ResetRound()
    {
        rc_obj[2].X = 312;
        rc_obj[2].Y = 232;
        mdx = (sbyte)(rnd.Next(0, 2) == 0 ? SPD_BALL : -SPD_BALL);
        mdy = (sbyte)(rnd.Next(0, 2) == 0 ? SPD_BALL : -SPD_BALL);
    }

    public void StartGame(string ip, bool host)
    {
        //Timers
        tm_draw = new FrmTmr();
        if(host)
            tm_draw.Tick += tm_draw_Tick2;
        else
            tm_draw.Tick += tm_draw_Tick;
        tm_draw.Interval = SPD_GAME;
        tm_draw.Start();
        tm_key = new FrmTmr();
        tm_key.Tick += tm_key_Tick;
        tm_key.Interval = SPD_GAME;
        tm_key.Start();
        try
        {
            if (host)
            {
                rnd = new Random();
                server = new TcpListener(System.Net.IPAddress.Any, 7777);
                server.Start();
                sock = server.AcceptSocket();
                server.Stop();
                plid = 0;
                thd_snd = new Thread(new ThreadStart(NetSnd2));
                thd_snd.Start();
                ResetRound();
            }
            else
            {
                client = new TcpClient(ip, 7777);
                sock = client.Client;
                plid = 1;
                thd_snd = new Thread(new ThreadStart(NetSnd));
                thd_snd.Start();
            }
        }
        catch (Exception e)
        {
            WriteError(e);
        }
        thd_rec = new Thread(new ThreadStart(NetRec));
        thd_rec.Start();
    }

    void NetRec()
    {
        try
        {
            byte[] buffer = new byte[5];
            short x, y;
            while (true)
            {
                sock.Receive(buffer);
                x = BitConverter.ToInt16(buffer, 1);
                y = BitConverter.ToInt16(buffer, 3);
                rc_obj[buffer[0]].X = x;
                rc_obj[buffer[0]].Y = y;
            }
        }
        catch{}
    }

    //Client side
    void NetSnd()
    {
        try
        {
            byte[] buffer = new byte[5];
            buffer[0] = 1;
            while (true)
            {
                Array.Copy(BitConverter.GetBytes(rc_obj[1].X), 0, buffer, 1, 2);
                Array.Copy(BitConverter.GetBytes(rc_obj[1].Y), 0, buffer, 3, 2);
                sock.Send(buffer);
                Thread.Sleep(SPD_GAME);
            }
        }
        catch (Exception e)
        {
            WriteError(e);
        }
    }

    //Server side
    void NetSnd2()
    {
        try
        {
            byte[] buffer = new byte[5];
            while (true)
            {
                buffer[0] = 0;
                Array.Copy(BitConverter.GetBytes(rc_obj[0].X), 0, buffer, 1, 2);
                Array.Copy(BitConverter.GetBytes(rc_obj[0].Y), 0, buffer, 3, 2);
                sock.Send(buffer);
                buffer[0] = 2;
                Array.Copy(BitConverter.GetBytes(rc_obj[2].X), 0, buffer, 1, 2);
                Array.Copy(BitConverter.GetBytes(rc_obj[2].Y), 0, buffer, 3, 2);
                sock.Send(buffer);
                Thread.Sleep(SPD_GAME);
            }
        }
        catch (Exception e)
        {
            WriteError(e);
        }
    }

    void WriteError(Exception e)
    {
        MessageBox.Show(e.Source + " - " + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        Environment.Exit(0);
    }

    protected override void OnClosed(EventArgs e)
    {
        Environment.Exit(0);
        base.OnClosed(e);
    }
}

class Form2 : Form
{
    Button bt_serv;
    Button bt_conn;
    TextBox tb_ip;
    Form1 frm;
    bool sel = false;

    public Form2(Form1 frm)
    {
        FormBorderStyle = FormBorderStyle.FixedSingle;
        ClientSize = new Size(280,48);
        StartPosition = FormStartPosition.CenterParent;
        ControlBox = false;
        this.frm = frm;
        Text = "Multiplayer menu";
        tb_ip = new TextBox();
        tb_ip.Bounds = new Rectangle(12,12,128,24);
        Controls.Add(tb_ip);
        bt_conn = new Button();
        bt_conn.Text = "Connect";
        bt_conn.Bounds = new Rectangle(tb_ip.Right,12,64,24);
        bt_conn.Click += bt_conn_Click;
        Controls.Add(bt_conn);
        bt_serv = new Button();
        bt_serv.Text = "Host";
        bt_serv.Bounds = new Rectangle(bt_conn.Right,12,64,24);
        bt_serv.Click += bt_serv_Click;
        Controls.Add(bt_serv);
    }

    void bt_conn_Click(object sender, EventArgs e)
    {
        Text = "Waiting...";
        frm.StartGame(tb_ip.Text, false);
        sel = true;
        this.Dispose();
    }

    void bt_serv_Click(object sender, EventArgs e)
    {
        Text = "Waiting...";
        frm.StartGame(null, true);
        sel = true;
        this.Dispose();
    }

    protected override void OnClosed(EventArgs e)
    {
        if(!sel)
            Environment.Exit(0);
        base.OnClosed(e);
    }
}

class Program
{
    [STAThread]
    static void Main()
    {
        Application.Run(new Form1());
    }
}