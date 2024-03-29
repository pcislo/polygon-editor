﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolygonEditor.Desktop.Models.Constraints
{
    public class VerticalEdgeConstraint : IVertexConstraint
    {
        private Vertex v1;
        private Vertex v2;


        public VerticalEdgeConstraint(Vertex v1, Vertex v2)
        {
            this.v1 = v1;
            this.v2 = v2;
        }

        public bool IsVertexInvolved(Vertex vertex)
        {
            return (v1 == vertex || v2 == vertex);
        }

        public bool IsContraintValid()
        {
            return v1.X == v2.X && v1.Y != v2.Y;
        }

        public bool TryRepairConstraint(Polygon polygon)
        {
            if (IsContraintValid())
                return true;

            if (v1.IsLocked && v2.IsLocked)
                return false;

            if (!v1.IsLocked)
            {
                polygon.SetVertexPosition(v1, v2.X, v1.Y);
            }
            else
            {
                polygon.SetVertexPosition(v2, v1.X, v2.Y);
            }

            return IsContraintValid();
        }

        public bool IsCollisionWithConstraints(IEnumerable<IVertexConstraint> otherConstraints)
        {
            foreach (var vertexConstraint in otherConstraints)
            {
                if(vertexConstraint.IsVertexInvolved(v1) && vertexConstraint is VerticalEdgeConstraint)
                    return true;
                if (vertexConstraint.IsVertexInvolved(v2) && vertexConstraint is VerticalEdgeConstraint)
                    return true;
            }

            return false;
        }

        public IEnumerable<Vertex> GetVertexes()
        {
            yield return v1;
            yield return v2;
        }
    }
}
