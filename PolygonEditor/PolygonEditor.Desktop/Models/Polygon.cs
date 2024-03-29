﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PolygonEditor.Desktop.Models.Constraints;

namespace PolygonEditor.Desktop.Models
{
    public class Polygon
    {
        private bool isMoveable;
        private readonly List<Vertex> vertexes;
        private readonly List<IVertexConstraint> constraints;

        public event Action<int, int> Moved;
        public event Action<Vertex> VertexAdded;
        public event Action<Vertex> VertexMoved;

        public bool AutoConstraints { get; set; }
        public bool IsClosed { get; private set; } = false;

        public Polygon(bool isMoveable = true)
        {
            this.isMoveable = isMoveable;
            vertexes = new List<Vertex>();
            constraints = new List<IVertexConstraint>();
        }

        public Vertex AddVertex(int x, int y)
        {
            var vertex = new Vertex(x, y);
            vertexes.Add(vertex);

            if (AutoConstraints)
            {
                for (int i = 0; i < vertexes.Count; i++)
                {
                    if (vertexes[i] == vertex)
                    {
                        if (i - 1 >= 0)
                        {
                            var v1 = vertexes[i - 1];
                            AddAutoConstraints(v1, vertex);
                        }

                        if (i + 1 < vertexes.Count)
                        {
                            var v1 = vertexes[i + 1];
                            AddAutoConstraints(v1, vertex);
                        }
                    }
                }
            }

            VertexAdded?.Invoke(vertex);
            return vertex;
        }

        private void AddAutoConstraints(Vertex v1, Vertex v2)
        {
            if (Math.Abs(v1.X - v2.X) < 5)
            {
                var constraint = new VerticalEdgeConstraint(v1, v2);
                constraints.Add(constraint);
                constraint.TryRepairConstraint(this);
            }
            if (Math.Abs(v1.Y - v2.Y) < 5)
            {
                var constraint = new HorizontalEdgeConstraint(v1, v2);
                constraints.Add(constraint);
                constraint.TryRepairConstraint(this);
            }
        }

        public Vertex AddVertex(int x, int y, Vertex v1, Vertex v2)
        {
            constraints.RemoveAll(c => c.IsVertexInvolved(v1) && c.IsVertexInvolved(v2));

            for (int i = 0; i < vertexes.Count; i++)
            {
                if (vertexes[i] == v1 && vertexes[(i + 1) % vertexes.Count] == v2)
                {
                    var vertex = new Vertex(x, y);
                    vertexes.Insert(i+1, vertex);
                    VertexAdded?.Invoke(vertex);
                    return vertex;
                }
            }

            return null;
        }

        public void AddConstraint(IVertexConstraint constraint)
        {
            if(constraint.IsCollisionWithConstraints(constraints))
                return;

            if (constraint.IsContraintValid() || constraint.TryRepairConstraint(this))
            {
                constraints.Add(constraint);
            }
        }

        public void RemoveConstraint(params Vertex[] vertexesGroup)
        {
            var constraintsToDelete = new List<IVertexConstraint>();
            foreach (var vertexConstraint in constraints)
            {
                bool isToDelete = true;
                foreach (var vertexInGroup in vertexesGroup)
                {
                    if (!vertexConstraint.IsVertexInvolved(vertexInGroup))
                    {
                        isToDelete = false;
                        break;
                    }
                }

                if(isToDelete)
                    constraintsToDelete.Add(vertexConstraint);
            }

            constraints.RemoveAll(x => constraintsToDelete.Contains(x));
        }

        public void SetVertexPosition(Vertex p, int x, int y)
        {
            if (x <= 0)
                x = 0;
            if (y <= 0)
                y = 0;

            p.IsLocked = true;

            int backupX = p.X;
            int backupY = p.Y;

            p.X = x;
            p.Y = y;

            foreach (var constraint in constraints)
            {
                if (constraint.IsVertexInvolved(p) && !constraint.IsContraintValid())
                {
                    if (!constraint.TryRepairConstraint(this))
                    {
                        p.X = backupX;
                        p.Y = backupY;
                    }
                }
            }

            if(p.X != backupX || p.Y != backupY)
                VertexMoved?.Invoke(p);

            p.IsLocked = false;
        }

        public void MovePolygon(int x, int y)
        {
            if (!isMoveable)
                return;

            int xDiff = 0;
            int yDiff = 0;

            foreach (var vertex in vertexes)
            {
                vertex.X += x;
                vertex.Y += y;

                if (vertex.X <= xDiff)
                    xDiff = vertex.X;

                if (vertex.Y <= yDiff)
                    yDiff = vertex.Y;
            }

            foreach (var vertex in vertexes)
            {
                vertex.X -= xDiff;
                vertex.Y -= yDiff;
            }

            Moved?.Invoke(x+xDiff, y+yDiff);
        }

        public bool RemoveVertex(int x, int y)
        {
            var vertex = GetVertex(x, y);
            vertexes.Remove(vertex);
            constraints.RemoveAll(c => c.IsVertexInvolved(vertex));

            return vertex != null;
        }

        public Vertex GetVertex(int x, int y)
        {
            return (from p in vertexes where p.IsNear(x, y) select p).FirstOrDefault();
        }

        public (Vertex p1, Vertex p2) GetVertexesBetween(int x, int y)
        {
            if (vertexes.Count < 2)
                return (null, null);

            for (int i = 0; i < vertexes.Count; i++)
            {
                if (IsBetweenVertexes(x, y, vertexes[i], vertexes[(i + 1) % vertexes.Count]))
                {
                    return (vertexes[i], vertexes[(i + 1) % vertexes.Count]);
                }
            }

            return (null, null);
        }

        public (Vertex v1, Vertex v2) GetNeighbours(Vertex middle)
        {
            Vertex prev = vertexes.Last();
            for (int i = 0; i < vertexes.Count; i++)
            {
                if (vertexes[i] == middle)
                {
                        return (prev, vertexes[(i + 1) % vertexes.Count]);
                }
                prev = vertexes[i];
            }

            return (null, null);
        }

        public IEnumerable<Vertex> GetVertexes()
        {
            return vertexes;
        }

        public IEnumerable<IVertexConstraint> GetConstraints()
        {
            return constraints;
        }

        public IEnumerable<(Vertex v1, Vertex v2)> GetEdges()
        {
            for (int i = 0; i < vertexes.Count; i++)
            {
                if(i+1 < vertexes.Count || IsClosed)
                yield return (vertexes[i], vertexes[(i + 1)%vertexes.Count]);
            }
        }

        public void SetClosed()
        {
            IsClosed = true;
        }

        private bool IsBetweenVertexes(int x, int y, Vertex p1, Vertex p2)
        {
            if ((x <= Math.Min(p1.X, p2.X) || x >= Math.Max(p1.X, p2.X)) && Math.Abs(p1.X-p2.X)>20)
                return false;
            if ((y <= Math.Min(p1.Y, p2.Y) || y >= Math.Max(p1.Y, p2.Y)) && Math.Abs(p1.Y-p2.Y)>20)
                return false;

            int vec = (x - p1.X) * (p2.Y - p1.Y) - (p2.X - p1.X) * (y - p1.Y);
            double dist = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            return (Math.Abs(vec) <= dist*10);
        }
    }
}
