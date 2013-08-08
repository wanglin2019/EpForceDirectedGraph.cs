﻿/*! 
@file ForceDirected.cs
@author Woong Gyu La a.k.a Chris. <juhgiyo@gmail.com>
		<http://github.com/juhgiyo/epForceDirectedGraph.cs>
@date August 08, 2013
@brief ForceDirected Interface
@version 1.0

@section LICENSE

Copyright (C) 2013  Woong Gyu La <juhgiyo@gmail.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

@section DESCRIPTION

An Interface for the ForceDirected Class.

*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace EpForceDirectedGraph
{

    public class NearestPoint{
            public NearestPoint()
            {
                node=null;
                point=null;
                distance=null;
            }
            public Node node;
            public Point point;
            public float? distance;
        }

    public class BoundingBox
    {
        public static float defaultBB= 2.0f;
        public static float defaultPadding = 0.07f; // ~5% padding
        
        public BoundingBox()
        {
            topRightBack = null;
            bottomLeftFront = null;
        }
        public AbstractVector topRightBack;
        public AbstractVector bottomLeftFront;
    }

    public abstract class ForceDirected<Vector> : IForceDirected where Vector : IVector
    {
        public int TimeStep{
            get;set;
        }
        Timer timer; 
    	public float Stiffness
        {
            get;
            protected set;
        }

        public float Repulsion
        {
            get;
            protected set;
        }

        public float Damping
        {
            get;
            protected set;
        }

        public bool Started{
            get;
            protected set;
        }

        protected bool Stopped
        {
            get;
            set;
        }

        public float Threadshold
        {
            get;
            set;
        }

        public float PhysicsTimeStep
        {
            get;
            set;
        }

        protected Dictionary<string, Point> nodePoints;
        protected Dictionary<string, Spring> edgeSprings;
        public IGraph graph
        {
            get;
            protected set;
        }
        protected IRendererForForceDirected renderer;

        public ForceDirected(IGraph iGraph, float iStiffness, float iRepulsion, float iDamping, int iTimeStep = 10)
        {
            TimeStep=iTimeStep;
            timer=new Timer(TimeStep);
            graph=iGraph;
            Stiffness=iStiffness;
            Repulsion=iRepulsion;
            Damping=iDamping;
            nodePoints = new Dictionary<string, Point>();
            edgeSprings = new Dictionary<string, Spring>();

            timer.Elapsed+=new ElapsedEventHandler(Step);

            renderer=null;

            Threadshold = 0.01f;
            PhysicsTimeStep = 0.03f;
        }

        protected abstract Point GetPoint(Node iNode);
        
        

        protected Spring GetSpring(Edge iEdge)
        {
            if(!(edgeSprings.ContainsKey(iEdge.ID)))
            {
                float length = iEdge.Data.length;
                Spring existingSpring = null;

                List<Edge> fromEdges= graph.GetEdges(iEdge.Source,iEdge.Target);
                foreach(Edge e in fromEdges)
                {
                    if(existingSpring==null && edgeSprings.ContainsKey(e.ID))
                    {
                        existingSpring=edgeSprings[e.ID];
                        break;
                    }
                }
                if(existingSpring!=null)
                {
                    return new Spring(existingSpring.point1, existingSpring.point2, 0.0f, 0.0f);
                }

                List<Edge> toEdges = graph.GetEdges(iEdge.Target,iEdge.Source);
                foreach(Edge e in toEdges)
                {
                    if(existingSpring==null && edgeSprings.ContainsKey(e.ID))
                    {
                        existingSpring=edgeSprings[e.ID];
                        break;
                    }
                }
                if(existingSpring!=null)
                {
                    return new Spring(existingSpring.point2, existingSpring.point1, 0.0f, 0.0f);
                }

                edgeSprings[iEdge.ID] = new Spring(GetPoint(iEdge.Source), GetPoint(iEdge.Target), length, Stiffness);
            }
            return edgeSprings[iEdge.ID];
        }

        protected void ApplyCoulombsLaw()
        {
            foreach(Node n1 in graph.nodes)
            {
                Point point1 = GetPoint(n1);
                foreach(Node n2 in graph.nodes)
                {
                    Point point2 = GetPoint(n2);
                    if(point1!=point2)
                    {
                        AbstractVector d=point1.position-point2.position;
                        float distance = d.Magnitude() +0.1f;
                        AbstractVector direction = d.Normalize();
                        point1.ApplyForce((direction*Repulsion)/(distance*distance*0.5f));
                        point2.ApplyForce((direction*Repulsion)/(distance*distance*-0.5f));
                    }
                }
            }
        }

        protected void ApplyHookesLaw()
        {
            foreach(Edge e in graph.edges)
            {
                Spring spring = GetSpring(e);
                AbstractVector d = spring.point2.position-spring.point1.position;
                float displacement = spring.Length-d.Magnitude();
                AbstractVector direction = d.Normalize();

                spring.point1.ApplyForce(direction*(spring.K * displacement * -0.5f));
                spring.point2.ApplyForce(direction*(spring.K * displacement * 0.5f));
            }
        }

        protected void AttractToCentre()
        {
            foreach(Node n in graph.nodes)
            {
                Point point = GetPoint(n);
                AbstractVector direction = point.position*-1.0f;
                point.ApplyForce(direction*(Repulsion/50.0f));
            }
        }

        protected void UpdateVelocity(float iTimeStep)
        {
            foreach(Node n in graph.nodes)
            {
                Point point = GetPoint(n);
                point.velocity.Add(point.acceleration*iTimeStep);
                point.velocity.Multiply(Damping);
                point.acceleration.SetZero();
            }
        }

        protected void UpdatePosition(float iTimeStep)
        {
            foreach(Node n in graph.nodes)
            {
                Point point = GetPoint(n);
                point.position.Add(point.velocity*iTimeStep);
            }
        }

        protected float TotalEnergy()
        {
            float energy=0.0f;
            foreach(Node n in graph.nodes)
            {
                Point point = GetPoint(n);
                float speed = point.velocity.Magnitude();
                energy+=0.5f *point.mass *speed*speed;
            }
            return energy;
        }

        protected  void Step(object sender, ElapsedEventArgs e)
        {
            ApplyCoulombsLaw();
            ApplyHookesLaw();
            AttractToCentre();
            UpdateVelocity(PhysicsTimeStep);
            UpdatePosition(PhysicsTimeStep);
            if(renderer !=null)
            {
                renderer.Draw();
            }

            if (Stopped || TotalEnergy() < Threadshold)
            {
                Started=false;
                if(renderer!=null)
                {
                    renderer.Done();
                }
            }
            else
            {
                timer.Enabled=true;
            }
        }


        public void Start(IRendererForForceDirected iRenderer)
        {
            if(Started)
                return;
            Started=true;
            Stopped=false;

            renderer=iRenderer;
            timer.Interval=TimeStep;
            timer.Enabled=true;
        }

        public void Stop()
        {
            Stopped=true;
        }

        public void EachEdge(EdgeAction del)
        {
            foreach(Edge e in graph.edges)
            {
                del(e, GetSpring(e));
            }
        }

        public void EachNode(NodeAction del)
        {
            foreach (Node n in graph.nodes)
            {
                del(n, GetPoint(n));
            }
        }

        public NearestPoint Nearest(AbstractVector position)
        {
            NearestPoint min = new NearestPoint();
            foreach(Node n in graph.nodes)
            {
                Point point = GetPoint(n);
                float distance = (point.position-position).Magnitude();
                if(min.distance==null || distance<min.distance)
                {
                    min.node=n;
                    min.point=point;
                    min.distance=distance;
                }
            }
            return min;
        }

        public abstract BoundingBox GetBoundingBox();
	
    }

    public class ForceDirected2D : ForceDirected<Vector2>
    {
        public ForceDirected2D(IGraph iGraph, float iStiffness, float iRepulsion, float iDamping, int iTimeStep = 10)
            : base(iGraph, iStiffness, iRepulsion, iDamping, iTimeStep)
        {

        }

        protected override Point GetPoint(Node iNode)
        {
            if (!(nodePoints.ContainsKey(iNode.ID)))
            {
                float mass = iNode.Data.mass;
                nodePoints[iNode.ID] = new Point(Vector2.Random(), Vector2.Zero(), Vector2.Zero(), mass);
            }
            return nodePoints[iNode.ID];
        }

        public override BoundingBox GetBoundingBox()
        {
            BoundingBox boundingBox = new BoundingBox();
            Vector2 bottomLeft = Vector2.Identity().Multiply(BoundingBox.defaultBB*-1.0f) as Vector2;
            Vector2 topRight= Vector2.Identity().Multiply(BoundingBox.defaultBB) as Vector2;
            foreach (Node n in graph.nodes)
            {
                Vector2 position=GetPoint(n).position as Vector2;

                if(position.x < bottomLeft.x)
                    bottomLeft.x=position.x;
                if(position.y<bottomLeft.y)
                    bottomLeft.y=position.y;
                if(position.x>topRight.x)
                    topRight.x=position.x;
                if(position.y>topRight.y)
                    topRight.y=position.y;
            }
            AbstractVector padding = (topRight-bottomLeft).Multiply(BoundingBox.defaultPadding);
            boundingBox.bottomLeftFront=bottomLeft.Subtract(padding);
            boundingBox.topRightBack=topRight.Add(padding);
            return boundingBox;

        }
    }

    public class ForceDirected3D : ForceDirected<Vector3>
    {
        public ForceDirected3D(IGraph iGraph, float iStiffness, float iRepulsion, float iDamping, int iTimeStep = 10)
            : base(iGraph, iStiffness, iRepulsion, iDamping, iTimeStep)
        {

        }

        protected override Point GetPoint(Node iNode)
        {
            if (!(nodePoints.ContainsKey(iNode.ID)))
            {
                float mass = iNode.Data.mass;
                nodePoints[iNode.ID] = new Point(Vector3.Random(), Vector3.Zero(), Vector3.Zero(), mass);
            }
            return nodePoints[iNode.ID];
        }

        public override BoundingBox GetBoundingBox()
        {
            BoundingBox boundingBox = new BoundingBox();
            Vector3 bottomLeft = Vector3.Identity().Multiply(BoundingBox.defaultBB * -1.0f) as Vector3;
            Vector3 topRight = Vector3.Identity().Multiply(BoundingBox.defaultBB) as Vector3;
            foreach (Node n in graph.nodes)
            {
                Vector3 position = GetPoint(n).position as Vector3;
                if (position.x < bottomLeft.x)
                    bottomLeft.x = position.x;
                if (position.y < bottomLeft.y)
                    bottomLeft.y = position.y;
                if (position.z<bottomLeft.z)
                    bottomLeft.z = position.z;
                if (position.x > topRight.x)
                    topRight.x = position.x;
                if (position.y > topRight.y)
                    topRight.y = position.y;
                if (position.z > topRight.z)
                    topRight.z = position.z;
            }
            AbstractVector padding = (topRight - bottomLeft).Multiply(BoundingBox.defaultPadding);
            boundingBox.bottomLeftFront = bottomLeft.Subtract(padding);
            boundingBox.topRightBack = topRight.Add(padding);
            return boundingBox;

        }
    }
}
