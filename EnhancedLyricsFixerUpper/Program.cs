using System.Text;

Console.WriteLine("Enter lyrics");
StringBuilder rl=new();
bool F = true;
StringBuilder proper = new();
do
{
    if (!F)
    {
        rl = rl.Replace('[', '<').Replace(']', '>');
        if (rl[0] ==('<'))
        {
            rl[0] = '[';
            for (int a = 0; a < rl.Length; a++)
            {
                if (rl[a] == '>')
                {
                    rl[a] = ']';
                    break;
                }
            }
        }
        proper.AppendLine(rl.ToString());
    }
    rl = new(Console.ReadLine());
    F = false;

}while(rl.Length!=0);
Console.WriteLine("FIXED");
Console.WriteLine(proper.ToString());