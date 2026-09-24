using DBDiver.Models;

namespace DBDiver.Services;

public class GraphLayoutService
{
    private const double CanvasPadding = 100;
    private const double CoolingRate = 0.98;
    private const double DefaultCardWidth = 180;
    private const double DefaultCardHeight = 70;
    private const double SemanticLabelScreenPadding = 12;
    private const int CollisionPassCount = 6;
    private const double MaxExtent = 6000;
    private const int MaxHubCount = 10;

    public void ComputeLayout(DbSchema schema, Dictionary<string, GraphNode> nodes)
        => ComputeLayout(schema, nodes, new GraphLayoutSettings());

    public void ComputeLayout(DbSchema schema, Dictionary<string, GraphNode> nodes, GraphLayoutSettings settings)
    {
        var tableNames = schema.Tables.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edgePairs = schema.Relationships
            .Where(r => tableNames.Contains(r.SourceTable) && tableNames.Contains(r.TargetTable))
            .Select(r => (Source: r.SourceTable, Target: r.TargetTable))
            .ToHashSet();

        var count = nodes.Count;
        var gridCols = (int)Math.Ceiling(Math.Sqrt(count));

        var adjacency = BuildAdjacency(edgePairs);
        var components = FindConnectedComponents(nodes.Keys, adjacency);
        var degrees = adjacency.ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.OrdinalIgnoreCase);
        var hubs = SelectHubs(components, degrees, MaxHubCount);
        var assignments = AssignNodesToClusters(nodes.Keys, edgePairs, hubs);

        foreach (var hub in hubs)
        {
            nodes[hub].Width = Math.Max(nodes[hub].Width, 220);
            nodes[hub].Height = Math.Max(nodes[hub].Height, 92);
            nodes[hub].IsHub = true;
        }

        var index = 0;
        foreach (var (name, node) in nodes)
        {
            var gridX = index % gridCols;
            var gridY = index / gridCols;
            node.X = CanvasPadding + gridX * (DefaultCardWidth + 60);
            node.Y = CanvasPadding + gridY * (DefaultCardHeight + 60);
            node.VelocityX = 0;
            node.VelocityY = 0;
            index++;
        }

        for (var iteration = 0; iteration < settings.MaxIterations; iteration++)
        {
            var maxDisplacement = 0.0;
            var temperature = 60 * Math.Pow(CoolingRate, iteration);

            foreach (var nodeA in nodes.Values)
            {
                double fx = 0, fy = 0;

                foreach (var nodeB in nodes.Values)
                {
                    if (nodeA == nodeB) continue;

                    var dx = nodeA.X - nodeB.X;
                    var dy = nodeA.Y - nodeB.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 1) dist = 1;

                    var repulsion = settings.RepulsionStrength / dist;
                    fx += repulsion * dx / dist;
                    fy += repulsion * dy / dist;
                }

                foreach (var (source, target) in edgePairs)
                {
                    var sourceNode = nodes.GetValueOrDefault(source);
                    var targetNode = nodes.GetValueOrDefault(target);
                    if (sourceNode is null || targetNode is null) continue;

                    if (sourceNode == nodeA || targetNode == nodeA)
                    {
                        var other = sourceNode == nodeA ? targetNode : sourceNode;
                        var dx = other.X - nodeA.X;
                        var dy = other.Y - nodeA.Y;
                        var dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < 1) dist = 1;

                        var displacement = dist - settings.SpringLength;
                        var springForce = settings.SpringStrength * displacement;
                        fx += springForce * dx / dist;
                        fy += springForce * dy / dist;
                    }
                }

                foreach (var (source, target) in edgePairs)
                {
                    var sourceNode = nodes.GetValueOrDefault(source);
                    var targetNode = nodes.GetValueOrDefault(target);
                    if (sourceNode is null || targetNode is null) continue;
                    if (sourceNode != nodeA && targetNode != nodeA) continue;
                    if (assignments[sourceNode.Table.Name] == assignments[targetNode.Table.Name]) continue;

                    var other = sourceNode == nodeA ? targetNode : sourceNode;
                    var dx = other.X - nodeA.X;
                    var dy = other.Y - nodeA.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 1) dist = 1;

                    var displacement = dist - settings.HubSpacing;
                    var springForce = settings.SpringStrength * displacement;
                    fx += springForce * dx / dist;
                    fy += springForce * dy / dist;
                }

                var moveX = fx * settings.Damping;
                var moveY = fy * settings.Damping;
                var magnitude = Math.Sqrt(moveX * moveX + moveY * moveY);
                if (magnitude > temperature)
                {
                    moveX = moveX / magnitude * temperature;
                    moveY = moveY / magnitude * temperature;
                }

                nodeA.VelocityX = moveX;
                nodeA.VelocityY = moveY;
                nodeA.X += nodeA.VelocityX;
                nodeA.Y += nodeA.VelocityY;

                var displacementMag = temperature;
                if (displacementMag > maxDisplacement)
                    maxDisplacement = displacementMag;
            }

            if (iteration % 20 == 19)
                ResolveOverlaps(nodes, settings.MinSeparation);

            if (maxDisplacement < 0.5)
                break;
        }

        ResolveClusterOverlap(nodes, hubs, assignments, settings.HubSpacing);
        ResolveOverlaps(nodes, settings.MinSeparation);
        ResolveSemanticOverlaps(nodes, settings.SemanticBuffer);

        var boundsMinX = nodes.Values.Min(n => n.X);
        var boundsMinY = nodes.Values.Min(n => n.Y);
        var boundsMaxX = nodes.Values.Max(n => n.X + n.Width);
        var boundsMaxY = nodes.Values.Max(n => n.Y + n.Height);
        var extent = Math.Max(boundsMaxX - boundsMinX, boundsMaxY - boundsMinY);
        if (extent > MaxExtent)
        {
            var scale = MaxExtent / extent;
            foreach (var node in nodes.Values)
            {
                node.X = boundsMinX + (node.X - boundsMinX) * scale;
                node.Y = boundsMinY + (node.Y - boundsMinY) * scale;
            }
            ResolveClusterOverlap(nodes, hubs, assignments, settings.HubSpacing);
            ResolveOverlaps(nodes, settings.MinSeparation);
            ResolveSemanticOverlaps(nodes, settings.SemanticBuffer);
        }

        var minX = nodes.Values.Min(n => n.X);
        var minY = nodes.Values.Min(n => n.Y);
        foreach (var node in nodes.Values)
        {
            node.X = CanvasPadding + (node.X - minX);
            node.Y = CanvasPadding + (node.Y - minY);
        }

        ResolveSemanticOverlaps(nodes, settings.SemanticBuffer);
    }

    private static Dictionary<string, List<string>> BuildAdjacency(
        HashSet<(string Source, string Target)> edgePairs)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, target) in edgePairs)
        {
            if (!adjacency.TryGetValue(source, out var sourceNeighbors))
            {
                sourceNeighbors = [];
                adjacency[source] = sourceNeighbors;
            }
            sourceNeighbors.Add(target);

            if (!adjacency.TryGetValue(target, out var targetNeighbors))
            {
                targetNeighbors = [];
                adjacency[target] = targetNeighbors;
            }
            targetNeighbors.Add(source);
        }

        return adjacency;
    }

    private static List<List<string>> FindConnectedComponents(
        IEnumerable<string> nodeNames,
        Dictionary<string, List<string>> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = new List<List<string>>();

        foreach (var name in nodeNames)
        {
            if (!visited.Add(name)) continue;

            var component = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(name);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var neighbor in adjacency.GetValueOrDefault(current, []))
                    if (visited.Add(neighbor))
                        queue.Enqueue(neighbor);
            }

            components.Add(component);
        }

        return components;
    }

    private static List<string> SelectHubs(
        List<List<string>> components,
        Dictionary<string, int> degrees,
        int maxHubCount)
    {
        var hubs = new List<string>();

        foreach (var component in components.OrderByDescending(c => c.Count))
            hubs.Add(component
                .OrderByDescending(name => degrees.GetValueOrDefault(name))
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .First());

        return hubs
            .OrderByDescending(name => degrees.GetValueOrDefault(name))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(maxHubCount)
            .ToList();
    }

    private static Dictionary<string, string> AssignNodesToClusters(
        IEnumerable<string> nodeNames,
        HashSet<(string Source, string Target)> edgePairs,
        List<string> hubs)
    {
        var assignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hubSet = hubs.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in nodeNames)
            assignments[name] = hubSet.Contains(name) ? name : string.Empty;

        foreach (var hub in hubs)
            assignments[hub] = hub;

        var frontier = hubs.ToList();
        while (frontier.Count > 0)
        {
            var nextFrontier = new List<string>();

            foreach (var current in frontier)
            {
                foreach (var (source, target) in edgePairs)
                {
                    var neighbor = source.Equals(current, StringComparison.OrdinalIgnoreCase) ? target :
                        target.Equals(current, StringComparison.OrdinalIgnoreCase) ? source : null;
                    if (neighbor is null || assignments[neighbor] != string.Empty) continue;

                    assignments[neighbor] = assignments[current];
                    nextFrontier.Add(neighbor);
                }
            }

            frontier = nextFrontier.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        foreach (var name in nodeNames.Where(name => assignments[name] == string.Empty))
            assignments[name] = name;

        return assignments;
    }

    private static void ResolveClusterOverlap(
        Dictionary<string, GraphNode> nodes,
        List<string> hubs,
        Dictionary<string, string> assignments,
        double hubSpacing)
    {
        if (hubs.Count < 2) return;

        foreach (var (hubA, hubB) in hubs.SelectMany((hubA, index) =>
                     hubs.Skip(index + 1).Select(hubB => (hubA, hubB))))
        {
            var dx = nodes[hubB].X - nodes[hubA].X;
            var dy = nodes[hubB].Y - nodes[hubA].Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var minimum = hubSpacing;

            if (distance >= minimum) continue;
            if (distance < 1)
            {
                dx = 1;
                dy = 0;
                distance = 1;
            }

            var push = (minimum - distance) / 2;
            nodes[hubA].X -= push * dx / distance;
            nodes[hubA].Y -= push * dy / distance;
            nodes[hubB].X += push * dx / distance;
            nodes[hubB].Y += push * dy / distance;
        }

        foreach (var hub in hubs)
        {
            var members = nodes.Values
                .Where(node => assignments[node.Table.Name] == hub)
                .ToList();
            if (members.Count == 0) continue;

            var hubNode = nodes[hub];
            var offsetX = hubNode.X - members.Average(node => node.X);
            var offsetY = hubNode.Y - members.Average(node => node.Y);
            foreach (var member in members)
            {
                member.X += offsetX;
                member.Y += offsetY;
            }
        }
    }

    private static void ResolveOverlaps(Dictionary<string, GraphNode> nodes, double minSeparation)
    {
        for (var pass = 0; pass < 50; pass++)
        {
            var resolved = true;
            var nodeList = nodes.Values.ToList();

            for (var i = 0; i < nodeList.Count; i++)
            {
                for (var j = i + 1; j < nodeList.Count; j++)
                {
                    var a = nodeList[i];
                    var b = nodeList[j];
                    var dx = b.X - a.X;
                    var dy = b.Y - a.Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy);

                    var minDist = minSeparation;
                    var widthSum = (a.Width + b.Width) / 2;
                    var heightSum = (a.Height + b.Height) / 2;
                    var absDx = Math.Abs(dx);
                    var absDy = Math.Abs(dy);

                    if (absDx < widthSum && absDy < heightSum)
                    {
                        resolved = false;
                        if (widthSum - absDx < heightSum - absDy)
                            dx = dx >= 0 ? widthSum - absDx : -(widthSum - absDx);
                        else
                            dy = dy >= 0 ? heightSum - absDy : -(heightSum - absDy);

                        dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist < 1) dist = 1;
                        var push = dist / 2;
                        a.X -= push * dx / dist;
                        a.Y -= push * dy / dist;
                        b.X += push * dx / dist;
                        b.Y += push * dy / dist;
                    }
                    else if (dist < minDist)
                    {
                        resolved = false;

                        if (dist < 1)
                        {
                            dx = (i % 2 == 0 ? 1 : -1) * minSeparation;
                            dy = 0;
                            dist = minSeparation;
                        }

                        var push = (minSeparation - dist) / 2;
                        a.X -= push * dx / dist;
                        a.Y -= push * dy / dist;
                        b.X += push * dx / dist;
                        b.Y += push * dy / dist;
                    }
                }
            }

            if (resolved) break;
        }
    }

    private static void ResolveSemanticOverlaps(Dictionary<string, GraphNode> nodes, double semanticBuffer)
    {
        var paddedNodes = nodes.Values
            .Select(node => (node, padding: GetSemanticPadding(node)))
            .ToList();

        for (var pass = 0; pass < CollisionPassCount; pass++)
        {
            var resolved = true;

            for (var i = 0; i < paddedNodes.Count; i++)
            {
                for (var j = i + 1; j < paddedNodes.Count; j++)
                {
                    var (a, paddingA) = paddedNodes[i];
                    var (b, paddingB) = paddedNodes[j];

                    var overlapX = (a.Width + b.Width) / 2 + paddingA + paddingB + semanticBuffer - Math.Abs(b.X - a.X);
                    var overlapY = (a.Height + b.Height) / 2 + paddingA + paddingB + semanticBuffer - Math.Abs(b.Y - a.Y);
                    if (overlapX <= 0 || overlapY <= 0)
                        continue;

                    resolved = false;
                    if (overlapX < overlapY)
                    {
                        var halfPush = overlapX / 2;
                        var direction = b.X >= a.X ? 1 : -1;
                        a.X -= halfPush * direction;
                        b.X += halfPush * direction;
                    }
                    else
                    {
                        var halfPush = overlapY / 2;
                        var direction = b.Y >= a.Y ? 1 : -1;
                        a.Y -= halfPush * direction;
                        b.Y += halfPush * direction;
                    }
                }
            }

            if (resolved)
                break;
        }
    }

    private static double GetSemanticPadding(GraphNode node)
    {
        var baseScale = Math.Clamp(node.Width / DefaultCardWidth, 1, 2.4);
        return (baseScale - 1) * DefaultCardHeight + SemanticLabelScreenPadding;
    }
}
