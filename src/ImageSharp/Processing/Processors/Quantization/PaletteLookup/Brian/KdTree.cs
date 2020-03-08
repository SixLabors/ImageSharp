using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization.PaletteLookup.Brian
{
    /// <summary>
    /// Baumstruktur für 2D Punkte. Ermöglicht effiziente Nearest-Neighbor Search
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public sealed class KdTree
    {
        public sealed class ColorWithIndex
        {
            public Vector4 Color;

            public int Index;

            public ColorWithIndex(Vector4 point, int index)
            {
                this.Color = point;
                this.Index = index;
            }
        }

        /// <summary>
        /// Knoten des KD-Baumes. Jeder Knoten besteht aus einem Punkt, der Tiefe des Knotens im Baum,
        /// einem linken und rechten unterknoten (Blätter), sowie einen Zeiger auf den Elternknoten.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public sealed class Node
        {
            /// <summary>
            /// Der Punkt
            /// </summary>
            public readonly ColorWithIndex Point;

            /// <summary>
            /// Die Tiefe des Knotens
            /// </summary>
            public readonly int Depth;

            /// <summary>
            /// Linker Kindknoten
            /// </summary>
            public Node Left;

            /// <summary>
            /// Rechter Kindknoten
            /// </summary>
            public Node Right;

            /// <summary>
            /// Elternknoten
            /// </summary>
            public readonly Node Parent;

            /// <summary>
            /// Initializes a new instance of the <see cref="Node"/> class.
            /// </summary>
            /// <param name="point">The point.</param>
            /// <param name="depth">The depth.</param>
            /// <param name="parent">The parent.</param>
            public Node(ColorWithIndex point, int depth, Node parent = null)
            {
                this.Point = point;
                this.Parent = parent;
                this.Depth = depth;
                this.Left = null;
                this.Right = null;
            }
        }

        /// <summary>
        /// Wurzelelement des Baums
        /// </summary>
        public readonly Node Root;

        /// <summary>
        /// Konstuktor, der aus einer Liste von Punkten einen Kd-Baum erstellt.
        /// </summary>
        /// <param name="pointList">Liste mit Punkten, aus denen der Baum erstellt werden soll.</param>
        public KdTree(IList<ColorWithIndex> pointList)
        {
            this.Root = BuildKdTree(pointList, 0);
        }

        /// <summary>
        /// Erstellt den kd-Baum.
        /// Vorgehensweise:
        /// 1. Sortieren der Punkte nach X Koordinate.
        /// 2. Bestimmen des Medians. Dieser wird in den Root Knoten aufgenommen.
        /// 3. Rekursiv wiederholen für die Werte die kleiner als der Medians und
        ///     grösser als der Median sind, wobei alternierend nach Y und X Koordinate
        ///     sortiert wird.
        ///     Ergebnis für die Werte kleiner des Medians wird linker Teilbaum und
        ///     Ergebnis für die Werte grösser des Medians wird rechter Teilbaum.
        /// Gerade Tiefen werden nach X Sortiert ungerade nach Y.
        /// </summary>
        private static Node BuildKdTree(IList<ColorWithIndex> pointList, int depth, Node parent = null)
        {
            if (pointList.Count is 0)
            {
                return null;
            }

            if (pointList.Count is 1)
            {
                var leaf = new Node(pointList[0], depth, parent: parent);
                return leaf;
            }

            // Liste sortieren
            if (depth % 3 is 0)
            {
                pointList = pointList.OrderBy(point => point, new PointComparerX()).ToList();
            }
            else if (depth % 3 is 1)
            {
                pointList = pointList.OrderBy(point => point, new PointComparerY()).ToList();
            }
            else
            {
                pointList = pointList.OrderBy(point => point, new PointComparerZ()).ToList();
            }

            // Median bestimmen
            int medianIndex = (pointList.Count - 1) / 2;

            // Alle Punkte links vom Median in eine Liste packen
            var pointListLeft = new List<ColorWithIndex>(pointList.Count / 2);
            for (int i = 0; i < medianIndex; ++i)
            {
                pointListLeft.Add(pointList[i]);
            }

            // Die restlichen Punkte in die rechte Liste packen
            var pointListRight = new List<ColorWithIndex>(pointList.Count / 2);
            for (int i = medianIndex + 1; i < pointList.Count; ++i)
            {
                pointListRight.Add(pointList[i]);
            }

            // aktuellen Knoten erzugen
            ColorWithIndex medianPoint = pointList[medianIndex];
            var node = new Node(medianPoint, depth, parent);

            // Linke und rechte Verzweigung mit den linken und rechten Teillisten erzeugen
            node.Left = BuildKdTree(pointListLeft, depth + 1, node);
            node.Right = BuildKdTree(pointListRight, depth + 1, node);

            return node;
        }

        /// <summary>
        /// Findet rekursiv den Knoten in den der Punkt p eingefügt werden soll
        /// </summary>
        /// <param name="p">Punkt p der eingefügt werden soll</param>
        /// <param name="tree">Aktueller Knoten</param>
        /// <returns>Knoten in den der Punkt p eingefügt werden soll</returns>
        public static Node FindInsertPlace(Vector4 p, Node tree)
        {
            while (true)
            {
                if (tree.Left is null && tree.Right is null)
                {
                    return tree;
                }

                // Prüfen, ob links oder rechts eingefügt werden muss (links, wenn p kleiner ist)
                bool pointIsSmaller = PointIsSmaller(p, tree);

                if (pointIsSmaller) // Punkt muss links eingefügt werden
                {
                    // Wenn der linke Knoten nicht besetzt ist, ist der aktuelle
                    // Knoten der Knoten an dem der Punkt p eingefügt werden muss
                    if (tree.Left is null)
                    {
                        return tree;
                    }

                    // Linker Teilbaum nicht leer, daher rekursiv weitersuchen
                    tree = tree.Left;
                    continue;
                }

                // Punkt p ist grösser und muss daher rechts eingefügt werden.
                // Wenn der rechte Knoten nicht besetzt ist, ist der aktuelle
                // Knoten der Knoten an dem der Punkt p eingefügt werden muss
                if (tree.Right is null)
                {
                    return tree;
                }

                // Rechter Teilbaum nicht leer, daher rekursiv weitersuchen
                tree = tree.Right;
            }
        }

        /// <summary>
        /// Findet den zu Punkt p am dichtesten liegenden Punkt aus dem kd-Baum
        /// </summary>
        /// <param name="p">Punkt, dessen nähester Nachbar gefunden werden soll</param>
        /// <param name="tree">Baum in dem gesucht werden soll</param>
        /// <param name="snnNode">Zweitbester Fund</param>
        /// <returns>nähester Nachbar</returns>
        public static Node FindNearestNeighbour(Vector4 p, Node tree, out Node snnNode)
        {
            // Den Elternknoten holen
            Node parentNode = tree.Parent;

            // Als erste Schätzung wird der Knoten genommen, an den der Punkt p
            // auch in den Baum eingefügt werden würde
            Node nnNode = FindInsertPlace(p, tree);

            // zweitbester Knoten ebenfalls initialisieren mit bester Schätzung
            snnNode = nnNode;

            // Die beste Schätzung ist, das Blatt der Stelle an der p eingefügt werden müsste
            if (nnNode.Left != null)
            {
                snnNode = nnNode;
                nnNode = nnNode.Left;
            }
            else if (nnNode.Right != null)
            {
                snnNode = nnNode;
                nnNode = nnNode.Right;
            }

            // Abbruchkriterium
            if (ReferenceEquals(nnNode.Parent, parentNode))
            {
                return nnNode;
            }

            // Distanz zum bisher besten Knoten
            double bestDistSquared = Vector4.DistanceSquared(nnNode.Point.Color, p);
            double secondBestDistSquared = Vector4.DistanceSquared(snnNode.Point.Color, p);

            // Baum nach oben gehen und nach dichteren Punkt suchen als der bisher gefundene
            bool rootReached = false;
            Node currentNode = nnNode.Parent;
            while (rootReached is false)
            {
                if (currentNode is null)
                {
                    break;
                }

                double distSquared = Vector4.DistanceSquared(currentNode.Point.Color, p);
                if (distSquared < bestDistSquared)
                {
                    // bisher besten Knoten als zweitbesten Knoten speichern
                    snnNode = nnNode;
                    secondBestDistSquared = bestDistSquared;

                    // aktueller Knoten ist besser als der bisher gefundene
                    nnNode = currentNode;
                    bestDistSquared = distSquared;
                }
                else if (distSquared < secondBestDistSquared)
                {
                    snnNode = currentNode;
                    secondBestDistSquared = distSquared;
                }

                // prüfen, ob die aktuelle Split-Gerade von einem Kreis mit dem Radius bestDistSquared geschnitten wird.
                double splittingCoordinateDiff = 0;
                if (currentNode.Depth % 3 is 0)
                {
                    splittingCoordinateDiff = currentNode.Point.Color.X - p.X;
                }
                else if (currentNode.Depth % 3 is 1)
                {
                    splittingCoordinateDiff = currentNode.Point.Color.Y - p.Y;
                }
                else
                {
                    splittingCoordinateDiff = currentNode.Point.Color.Z - p.Z;
                }

                if ((splittingCoordinateDiff * splittingCoordinateDiff) < bestDistSquared || (splittingCoordinateDiff * splittingCoordinateDiff) < secondBestDistSquared)
                {
                    // Split-Gerade wir geschnitten. Es könnte daher in einem Teilbaum ein besserer Knoten sein.
                    // Den Teilbaum durchsuchen, der weiter weg ist
                    Node furtherTree;
                    if (currentNode.Depth % 3 is 0)
                    {
                        // umgekehrt vorgehen, wie beim einfügen eines Punktes. D.h. wenn p.X kleiner dann rechts, ansonsten links
                        furtherTree = (p.X < currentNode.Point.Color.X) ? currentNode.Right : currentNode.Left;
                    }
                    else if (currentNode.Depth % 3 is 1)
                    {
                        // umgekehrt vorgehen, wie beim einfügen eines Punktes. D.h. wenn p.Y kleiner dann rechts, ansonsten links
                        furtherTree = (p.Y < currentNode.Point.Color.Y) ? currentNode.Right : currentNode.Left;
                    }
                    else
                    {
                        furtherTree = (p.Z < currentNode.Point.Color.Z) ? currentNode.Right : currentNode.Left;
                    }

                    // Den Teilbaum durchsuchen, der weiter weg ist
                    if (furtherTree != null)
                    {
                        Node snnNodeSubTree;
                        Node bestNodeInSubTree = FindNearestNeighbour(p, furtherTree, out snnNodeSubTree);

                        double bestDistInSubTreeSquared = Vector4.DistanceSquared(bestNodeInSubTree.Point.Color, p);
                        if (bestDistInSubTreeSquared < bestDistSquared)
                        {
                            // bisher besten Knoten als zweitbesten Knoten speichern
                            snnNode = nnNode;
                            secondBestDistSquared = bestDistSquared;

                            // aktueller Knoten ist besser als der bisher gefundene
                            nnNode = bestNodeInSubTree;
                            bestDistSquared = bestDistInSubTreeSquared;
                        }
                        else if (bestDistInSubTreeSquared < secondBestDistSquared)
                        {
                            snnNode = bestNodeInSubTree;
                            secondBestDistSquared = bestDistInSubTreeSquared;
                        }

                        // prüfen, ob das zweitbeste Ergebnis aus dem Teilbaum besser ist, als bisheruges zweitbestes Ergebnis.
                        if (bestNodeInSubTree != snnNodeSubTree)
                        {
                            double secondBestDistSquaredSubTree = Vector4.DistanceSquared(snnNodeSubTree.Point.Color, p);
                            if (secondBestDistSquaredSubTree < secondBestDistSquared)
                            {
                                snnNode = snnNodeSubTree;
                                secondBestDistSquared = secondBestDistSquaredSubTree;
                            }
                        }
                    }
                }

                // prüfen, ob wir im Root Knoten angekommen sind
                if (ReferenceEquals(currentNode.Parent, parentNode))
                {
                    rootReached = true;
                    //return nnNode;
                }

                // weiter nach oben gehen
                currentNode = currentNode.Parent;
            }

            return nnNode;
        }

        /// <summary>
        /// Geht den Baum rekursiv durch und gibt alle Punkte als Liste zurück, die in ihm enthalten sind.
        /// </summary>
        /// <param name="rootNode"></param>
        /// <returns></returns>
        private List<Vector4> GetPointList(Node rootNode)
        {
            var pointList = new List<Vector4>();
            if (rootNode is null)
            {
                return pointList;
            }

            pointList.Add(rootNode.Point.Color);

            // Liste des linken Teilbaums generieren
            List<Vector4> leftList = this.GetPointList(rootNode.Left);
            pointList.AddRange(leftList);

            // Liste des rechten Teilbaums generieren
            List<Vector4> rightList = this.GetPointList(rootNode.Right);
            pointList.AddRange(rightList);

            return pointList;
        }

        /// <summary>
        /// Geht den Baum rekursiv durch und gibt alle Punkte als Liste zurück, die in ihm enthalten sind.
        /// </summary>
        /// <returns></returns>
        public List<Vector4> GetPointList()
        {
            return this.GetPointList(this.Root);
        }

        private static bool PointIsSmaller(Vector4 p, Node tree)
        {
            if (tree is null)
            {
                return false;
            }

            bool pointIsSmaller;
            if (tree.Depth % 3 is 0)
            {
                // Punkte nach X Komponente vergleichen
                pointIsSmaller = p.X < tree.Point.Color.X;
            }
            else if (tree.Depth % 3 is 1)
            {
                // Punkte nach Y Komponente vergleichen
                pointIsSmaller = p.Y < tree.Point.Color.Y;
            }
            else
            {
                // Punkte nach Z Komponente vergleichen
                pointIsSmaller = p.Z < tree.Point.Color.Z;
            }

            return pointIsSmaller;
        }
    }

    /// <summary>
    /// Sortiert Punkte nach der X-Komponente (gefolgt von Y un dann Z).
    /// </summary>
    public sealed class PointComparerX : IComparer<KdTree.ColorWithIndex>
    {
        /// <summary>
        /// Compares the specified p1.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="p2">The p2.</param>
        /// <returns></returns>
        public int Compare(KdTree.ColorWithIndex p1, KdTree.ColorWithIndex p2)
        {
            // X-Werte vergleichen
            if (p1.Color.X > p2.Color.X)
            {
                return 1;
            }

            if (p1.Color.X < p2.Color.X)
            {
                return -1;
            }

            // X-Werte sind identisch
            // Y-Werte vergleichen
            if (p1.Color.Y > p2.Color.Y)
            {
                return 1;
            }

            if (p1.Color.Y < p2.Color.Y)
            {
                return -1;
            }

            // Y-Werte sind identisch
            // Z-Werte vergleichen
            if (p1.Color.Z > p2.Color.Z)
            {
                return 1;
            }

            if (p1.Color.Z < p2.Color.Z)
            {
                return -1;
            }

            // Alle Werte sind identisch.
            return 0;
        }
    }

    /// <summary>
    /// Sortiert Punkte nach der Y-Komponente (gefolgt von X und dann Z)
    /// </summary>
    public sealed class PointComparerY : IComparer<KdTree.ColorWithIndex>
    {
        /// <summary>
        /// Compares the specified p1.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="p2">The p2.</param>
        /// <returns></returns>
        public int Compare(KdTree.ColorWithIndex p1, KdTree.ColorWithIndex p2)
        {
            // Y-Werte vergleichen
            if (p1.Color.Y > p2.Color.Y)
            {
                return 1;
            }

            if (p1.Color.Y < p2.Color.Y)
            {
                return -1;
            }

            // Y-Werte sind identisch
            // X-Werte vergleichen
            if (p1.Color.X > p2.Color.X)
            {
                return 1;
            }

            if (p1.Color.X < p2.Color.X)
            {
                return -1;
            }

            // Y-Werte sind identisch
            // Z-Werte vergleichen
            if (p1.Color.Z > p2.Color.Z)
            {
                return 1;
            }

            if (p1.Color.Z < p2.Color.Z)
            {
                return -1;
            }

            // Alle Werte sind identisch.
            return 0;
        }
    }

    /// <summary>
    /// Sortiert Punkte nach der Z-Komponente
    /// </summary>
    public sealed class PointComparerZ : IComparer<KdTree.ColorWithIndex>
    {
        /// <summary>
        /// Compares the specified p1.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="p2">The p2.</param>
        /// <returns></returns>
        public int Compare(KdTree.ColorWithIndex p1, KdTree.ColorWithIndex p2)
        {
            if (p1.Color.Z > p2.Color.Z)
            {
                return 1;
            }

            if (p1.Color.Z < p2.Color.Z)
            {
                return -1;
            }

            if (p1.Color.X > p2.Color.X)
            {
                return 1;
            }

            if (p1.Color.X < p2.Color.X)
            {
                return -1;
            }

            if (p1.Color.Y > p2.Color.Y)
            {
                return 1;
            }

            if (p1.Color.Y < p2.Color.Y)
            {
                return -1;
            }

            // Alle Werte sind identisch.
            return 0;
        }
    }
}
