using System.Text;

static void Clear(int x, int y, int width, int height)
{
    int curTop = Console.CursorTop;
    int curLeft = Console.CursorLeft;
    for (; height > 0;)
    {
        Console.SetCursorPosition(x, y + --height);
        Console.Write(new string(' ', width));
    }
    Console.SetCursorPosition(curLeft, curTop);
}
char StraightPipe = '─';
char VerticalStraightPipe = '│';

char BendingTopLeft = '┌';
char BendingTopRight = '┐';
char BendingBottomLeft = '└';
char BendingBottomRight = '┘';
Console.WriteLine(BendingTopLeft + "" + BendingTopRight);
Console.WriteLine(BendingBottomLeft + "" + BendingBottomRight);

var progress = 0;
while (true)
{
    //Console.Clear();

    StringBuilder topline = new(BendingTopLeft);
    for (int i = 0; i < Console.WindowWidth - 2; i++)
    {
        topline.Append(StraightPipe);
    }
    topline.Append(BendingTopRight);
    StringBuilder bottomline = new(BendingBottomLeft);
    for (int i = 0; i < Console.WindowWidth - 2; i++)
    {
        topline.Append(StraightPipe);
    }
    topline.Append(BendingBottomRight);
    char Eighth = '░';
    char Quarter = '▒';
    char Half = '▓';
    char Full = '█';
    StringBuilder emptyline = new(VerticalStraightPipe);
    for (int i = 0; i < ((Console.WindowWidth - 2) / 100d) * progress; i++)
    {
        emptyline.Append(Eighth);
    }
    for (int i = (int)(((Console.WindowWidth - 2) / 100d) * progress); i < Console.WindowWidth - 3; i++)
    {
        emptyline.Append(' ');
    }
    emptyline.Append(VerticalStraightPipe);

    Console.WriteLine(topline.ToString());
    Console.WriteLine(emptyline.ToString());
    Console.WriteLine(bottomline.ToString());
    Thread.Sleep(100);
    progress++;
    if (progress > 100)
    {
        progress = 0;
    }
}