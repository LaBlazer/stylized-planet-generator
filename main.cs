using Godot;
using System;
using System.Collections.Generic;

public class main : WorldEnvironment
{
    private ProgressBar Progress;
    private Mesh OriginalMesh;
    private Random Rand = new Random(OS.GetTicksMsec());

    private const int MaxIterations = 145;
    private const int Sharpness = 50;

    private const float SeaLevel = 0.9f;
    private const float BeachLevel = 1f;
    private const float VegetationLevel = 1.02f;
    private const float MountainLevel = 1.1f;

    public override void _Ready()
    {
        Progress = GetNode<ProgressBar>("container/progress");
        OriginalMesh = GetNode<MeshInstance>("planet/Planet").Mesh;

        SetProcessInput(true);
        SetProcess(true);

        MakePlanet();
    }

    public override void _Process(float delta)
    {
        GetNode<Spatial>("cam_root").RotateY(delta / 3);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept") && !Progress.IsVisible())
            MakePlanet();

        if (@event.IsAction("sw_up"))
            GetNode<Camera>("cam_root/cam").TranslateObjectLocal(new Vector3(0, 0, -0.1f));
        else if (@event.IsAction("sw_down"))
            GetNode<Camera>("cam_root/cam").TranslateObjectLocal(new Vector3(0, 0, 0.1f));

    }

    public async void MakePlanet()
    {
        Progress.MaxValue = MaxIterations;
        Progress.Show();

        var surface = new MeshDataTool();
        surface.CreateFromSurface((ArrayMesh)OriginalMesh, 0);

        for (int i = 0; i < MaxIterations; i++)
        {
            await ToSignal(GetTree(), "idle_frame");

            Progress.Value = i;

            var dir = new Vector3(GetRandomRange(-1, 1), GetRandomRange(-1, 1), GetRandomRange(-1, 1)).Normalized();

            for (int j = 0; j < surface.GetVertexCount(); j++)
            {
                var v = surface.GetVertex(j);
                var norm = surface.GetVertexNormal(j);

                float dot = norm.Normalized().Dot(dir);
                dot = (float)(Math.Exp(dot * Sharpness) / (Math.Exp(dot * Sharpness) + 1) - 0.5);

                v += norm * dot * 0.01f;

                surface.SetVertex(j, v);
            }
        }

        for (int i = 0; i < surface.GetVertexCount(); i++)
        {
            var v = surface.GetVertex(i);
            var dist = v.Length();
            var distNormalized = RangeLerp(SeaLevel, MountainLevel, 0, 1, dist);

            var uv = new Vector2(distNormalized, 0);
            surface.SetVertexUv(i, uv);
        }

        for (int i = 0; i < surface.GetFaceCount(); i++)
        {
            var v1i = surface.GetFaceVertex(i, 0);
            var v2i = surface.GetFaceVertex(i, 1);
            var v3i = surface.GetFaceVertex(i, 2);

            var v1 = surface.GetVertex(v1i);
            var v2 = surface.GetVertex(v2i);
            var v3 = surface.GetVertex(v3i);

            var normal = -(v2 - v1).Normalized().Cross((v3 - v1).Normalized()).Normalized();

            surface.SetVertexNormal(v1i, normal);
            surface.SetVertexNormal(v2i, normal);
            surface.SetVertexNormal(v3i, normal);
        }

        ArrayMesh genmesh = new ArrayMesh();
        surface.CommitToSurface(genmesh);
        GetNode<MeshInstance>("planet/Planet").Mesh = genmesh;

        // Place trees

        List<Tuple<Vector3, Vector3>> treePairs = new List<Tuple<Vector3, Vector3>>();
        var treeMesh = (ResourceLoader.Load("res://tree.dae") as PackedScene).Instance().GetNode<MeshInstance>("tree").Mesh;
        var multiMesh = GetNode<MultiMeshInstance>("planet/trees").Multimesh;
        var planet = GetNode<Spatial>("planet");

        for (int i = 0; i < surface.GetVertexCount(); i++)
        {
            var v = surface.GetVertex(i);
            var dist = v.Length();

            var normal = surface.GetVertexNormal(i);

            var chance = 1 / (1 + Math.Pow(Math.Abs(dist - VegetationLevel) * 10, 2) * 10000);
            var isUnderwater = dist <= BeachLevel;

            if (!isUnderwater && GetRandomRange(0, 1) < chance)
                treePairs.Add(new Tuple<Vector3, Vector3>(v, normal));
        }

        multiMesh.Mesh = treeMesh;
        multiMesh.InstanceCount = treePairs.Count;

        for (int i = 0; i < treePairs.Count; i++)
        {
            var pos = planet.ToGlobal(treePairs[i].Item1);
            var normal = treePairs[i].Item2;

            var y = normal;
            var x = normal.Cross(new Vector3(0, 1, 0)).Normalized();
            var z = x.Cross(y).Normalized();
            var basis = new Basis(x, y, z).Rotated(y, GetRandomRange(0, 3.1415f * 2.0f));

            basis = basis.Scaled(new Vector3(1, 1, 1) * GetRandomRange(0.01f, 0.03f) / 2);

            multiMesh.SetInstanceTransform(i, new Transform(basis, pos));
        }

        Progress.Hide();
    }

    float RangeLerp(float from, float to, float min, float max, float weight)
    {
        return Mathf.Lerp(min, max, Mathf.InverseLerp(from, to, weight));
    }

    float GetRandomRange(float min, float max)
    {
        return min + ((float)Rand.NextDouble() * (max - min));
    }
}
