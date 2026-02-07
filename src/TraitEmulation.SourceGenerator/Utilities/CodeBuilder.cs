using System.Text;

namespace TraitEmulation.SourceGenerator.Utilities
{
    internal sealed class CodeBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indent;

        public void AppendLine(string line = "")
        {
            if (string.IsNullOrEmpty(line))
            {
                _sb.AppendLine();
            }
            else
            {
                _sb.Append(new string(' ', _indent * 4));
                _sb.AppendLine(line);
            }
        }

        public void Append(string text)
        {
            _sb.Append(text);
        }

        public void OpenBrace()
        {
            AppendLine("{");
            _indent++;
        }

        public void CloseBrace()
        {
            _indent--;
            AppendLine("}");
        }

        public override string ToString() => _sb.ToString();
    }
}
