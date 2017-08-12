using System.Collections.Generic;

namespace GenerateCrestronModule
    {
    public class Trie
        {
        public class Node
            {
            public char Char { get; private set; }
            public List<Node> Children { get; private set; }
            public Node Parent { get; private set; }
            public int Index { get; private set; }

            public Node(char ch, int index, Node parent)
                {
                this.Char = ch;
                this.Children = new List<Node>();
                this.Index = index;
                this.Parent = parent;
                }

            public Node FindChildNode(char c)
                {
                foreach (var child in this.Children)
                    if (child.Char == c)
                        return child;

                return null;
                }

            public override string ToString()
                {
                Node node = this;

                if (node.Char == '\0')
                    node = node.Parent;

                char[] chars = new char[node.Index + 1];

                while (node.Index > 0)
                    {
                    chars[node.Index] = node.Char;
                    node = node.Parent;
                    }

                return new string(chars);
                }
            }

        private readonly Node root;

        public Trie()
            {
            root = new Node('^', -1, null);
            }

        public Node FurthestMatchedNode(string str)
            {
            Node node = root;

            foreach (char ch in str)
                {
                Node childNode = node.FindChildNode(ch);

                if (childNode == null)
                    break;

                node = childNode;
                }

            return node;
            }

        public void Insert(string str)
            {
            Node node = FurthestMatchedNode(str);

            for (int i = node.Index + 1; i < str.Length; i++)
                {
                var newNode = new Node(str[i], i, node);
                node.Children.Add(newNode);
                node = newNode;
                }

            node.Children.Add(new Node('\0', node.Index + 1, node));
            }
        }
    }
