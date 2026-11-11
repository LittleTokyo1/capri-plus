namespace CapriDesktop
{
    internal sealed class Porter
    {
        private char[] b;
        private int j, k;

        internal static string Stem(string word)
        {
            if (word == null || word.Length < 3) return word;
            for (int at = 0; at < word.Length; at++)
                if (word[at] < 'a' || word[at] > 'z') return word;
            return new Porter().Run(word);
        }

        private string Run(string word)
        {
            b = word.ToCharArray();
            k = b.Length - 1;
            if (k > 1)
            {
                Step1();
                Step2();
                Step3();
                Step4();
                Step5();
                Step6();
            }
            return new string(b, 0, k + 1);
        }

        private bool Cons(int i)
        {
            switch (b[i])
            {
                case 'a': case 'e': case 'i': case 'o': case 'u': return false;
                case 'y': return i == 0 ? true : !Cons(i - 1);
                default: return true;
            }
        }

        private int M()
        {
            int n = 0;
            int i = 0;
            while (true)
            {
                if (i > j) return n;
                if (!Cons(i)) break;
                i++;
            }
            i++;
            while (true)
            {
                while (true)
                {
                    if (i > j) return n;
                    if (Cons(i)) break;
                    i++;
                }
                i++;
                n++;
                while (true)
                {
                    if (i > j) return n;
                    if (!Cons(i)) break;
                    i++;
                }
                i++;
            }
        }

        private bool VowelInStem()
        {
            for (int i = 0; i <= j; i++)
                if (!Cons(i)) return true;
            return false;
        }

        private bool Double(int at)
        {
            if (at < 1) return false;
            if (b[at] != b[at - 1]) return false;
            return Cons(at);
        }

        private bool Cvc(int i)
        {
            if (i < 2 || !Cons(i) || Cons(i - 1) || !Cons(i - 2)) return false;
            char ch = b[i];
            return ch != 'w' && ch != 'x' && ch != 'y';
        }

        private bool Ends(string s)
        {
            int l = s.Length;
            int o = k - l + 1;
            if (o < 0) return false;
            for (int i = 0; i < l; i++)
                if (b[o + i] != s[i]) return false;
            j = k - l;
            return true;
        }

        private void SetTo(string s)
        {
            int l = s.Length;
            int o = j + 1;
            if (o + l > b.Length)
            {
                char[] wider = new char[o + l];
                System.Array.Copy(b, wider, b.Length);
                b = wider;
            }
            for (int i = 0; i < l; i++) b[o + i] = s[i];
            k = j + l;
        }

        private void R(string s)
        {
            if (M() > 0) SetTo(s);
        }

        private void Step1()
        {
            if (b[k] == 's')
            {
                if (Ends("sses")) k -= 2;
                else if (Ends("ies")) SetTo("i");
                else if (b[k - 1] != 's') k--;
            }
            if (Ends("eed"))
            {
                if (M() > 0) k--;
            }
            else if ((Ends("ed") || Ends("ing")) && VowelInStem())
            {
                k = j;
                if (Ends("at")) SetTo("ate");
                else if (Ends("bl")) SetTo("ble");
                else if (Ends("iz")) SetTo("ize");
                else if (Double(k))
                {
                    k--;
                    char ch = b[k];
                    if (ch == 'l' || ch == 's' || ch == 'z') k++;
                }
                else if (M() == 1 && Cvc(k)) SetTo("e");
            }
        }

        private void Step2()
        {
            if (Ends("y") && VowelInStem()) b[k] = 'i';
        }

        private void Step3()
        {
            if (k == 0) return;
            switch (b[k - 1])
            {
                case 'a':
                    if (Ends("ational")) { R("ate"); break; }
                    if (Ends("tional")) { R("tion"); break; }
                    break;
                case 'c':
                    if (Ends("enci")) { R("ence"); break; }
                    if (Ends("anci")) { R("ance"); break; }
                    break;
                case 'e':
                    if (Ends("izer")) { R("ize"); break; }
                    break;
                case 'l':
                    if (Ends("bli")) { R("ble"); break; }
                    if (Ends("alli")) { R("al"); break; }
                    if (Ends("entli")) { R("ent"); break; }
                    if (Ends("eli")) { R("e"); break; }
                    if (Ends("ousli")) { R("ous"); break; }
                    break;
                case 'o':
                    if (Ends("ization")) { R("ize"); break; }
                    if (Ends("ation")) { R("ate"); break; }
                    if (Ends("ator")) { R("ate"); break; }
                    break;
                case 's':
                    if (Ends("alism")) { R("al"); break; }
                    if (Ends("iveness")) { R("ive"); break; }
                    if (Ends("fulness")) { R("ful"); break; }
                    if (Ends("ousness")) { R("ous"); break; }
                    break;
                case 't':
                    if (Ends("aliti")) { R("al"); break; }
                    if (Ends("iviti")) { R("ive"); break; }
                    if (Ends("biliti")) { R("ble"); break; }
                    break;
                case 'g':
                    if (Ends("logi")) { R("log"); break; }
                    break;
            }
        }

        private void Step4()
        {
            switch (b[k])
            {
                case 'e':
                    if (Ends("icate")) { R("ic"); break; }
                    if (Ends("ative")) { R(""); break; }
                    if (Ends("alize")) { R("al"); break; }
                    break;
                case 'i':
                    if (Ends("iciti")) { R("ic"); break; }
                    break;
                case 'l':
                    if (Ends("ical")) { R("ic"); break; }
                    if (Ends("ful")) { R(""); break; }
                    break;
                case 's':
                    if (Ends("ness")) { R(""); break; }
                    break;
            }
        }

        private void Step5()
        {
            if (k == 0) return;
            switch (b[k - 1])
            {
                case 'a':
                    if (Ends("al")) break;
                    return;
                case 'c':
                    if (Ends("ance")) break;
                    if (Ends("ence")) break;
                    return;
                case 'e':
                    if (Ends("er")) break;
                    return;
                case 'i':
                    if (Ends("ic")) break;
                    return;
                case 'l':
                    if (Ends("able")) break;
                    if (Ends("ible")) break;
                    return;
                case 'n':
                    if (Ends("ant")) break;
                    if (Ends("ement")) break;
                    if (Ends("ment")) break;
                    if (Ends("ent")) break;
                    return;
                case 'o':
                    if (Ends("ion") && j >= 0 && (b[j] == 's' || b[j] == 't')) break;
                    if (Ends("ou")) break;
                    return;
                case 's':
                    if (Ends("ism")) break;
                    return;
                case 't':
                    if (Ends("ate")) break;
                    if (Ends("iti")) break;
                    return;
                case 'u':
                    if (Ends("ous")) break;
                    return;
                case 'v':
                    if (Ends("ive")) break;
                    return;
                case 'z':
                    if (Ends("ize")) break;
                    return;
                default:
                    return;
            }
            if (M() > 1) k = j;
        }

        private void Step6()
        {
            j = k;
            if (b[k] == 'e')
            {
                int a = M();
                if (a > 1 || a == 1 && !Cvc(k - 1)) k--;
            }
            if (b[k] == 'l' && Double(k) && M() > 1) k--;
        }
    }
}
