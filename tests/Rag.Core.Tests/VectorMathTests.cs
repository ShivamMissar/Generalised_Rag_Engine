using Rag.Core.Similarity;

namespace Rag.Core.Tests;

public class VectorMathTests
{
    private const int Precision = 5;

    [Fact]
    public void Identical_vectors_score_one()
    {
        float[] v = [0.3f, -1.2f, 4.5f];

        Assert.Equal(1f, VectorMath.CosineSimilarity(v, v), Precision);
    }

    [Fact]
    public void Perpendicular_vectors_score_zero()
    {
        Assert.Equal(0f, VectorMath.CosineSimilarity([1f, 0f], [0f, 1f]), Precision);
    }

    [Fact]
    public void Opposite_vectors_score_minus_one()
    {
        Assert.Equal(-1f, VectorMath.CosineSimilarity([1f, 2f, 3f], [-1f, -2f, -3f]), Precision);
    }

    [Fact]
    public void Length_does_not_affect_score()
    {
        // Same direction, ten times longer.
        Assert.Equal(1f, VectorMath.CosineSimilarity([1f, 2f, 3f], [10f, 20f, 30f]), Precision);
    }

    [Fact]
    public void Vectors_at_45_degrees_score_cos_45()
    {
        Assert.Equal(MathF.Sqrt(2) / 2, VectorMath.CosineSimilarity([1f, 0f], [1f, 1f]), Precision);
    }

    [Fact]
    public void Score_is_symmetric()
    {
        float[] a = [0.2f, 0.7f, -0.1f];
        float[] b = [0.9f, -0.3f, 0.4f];

        Assert.Equal(VectorMath.CosineSimilarity(a, b), VectorMath.CosineSimilarity(b, a), Precision);
    }

    [Fact]
    public void Different_lengths_throw_with_both_lengths_in_message()
    {
        var ex = Assert.Throws<ArgumentException>(() => VectorMath.CosineSimilarity([1f, 2f, 3f], [1f, 2f]));

        Assert.Contains("3", ex.Message);
        Assert.Contains("2", ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Zero_vector_throws(bool zeroFirst)
    {
        float[] zero = [0f, 0f, 0f];
        float[] other = [1f, 2f, 3f];

        Assert.Throws<ArgumentException>(() =>
            zeroFirst ? VectorMath.CosineSimilarity(zero, other) : VectorMath.CosineSimilarity(other, zero));
    }
}
