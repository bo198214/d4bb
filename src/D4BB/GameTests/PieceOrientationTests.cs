using System.Collections.Generic;
using NUnit.Framework;
using D4BB.Comb;
using D4BB.Geometry;
using D4BB.Transforms;

namespace D4BB.Game
{
    [TestFixture]
    public class PieceOrientationTests
    {
        // Apply a rotation path the same way BestViewRotation / GameLevel.RotateSelected do:
        // each 90° step pivots about the piece's current asCubes centroid.
        static int[][] ApplyPath(int[][] origins, List<(int v, int w)> path)
        {
            var cur = IntegerOps.Clone(origins);
            foreach (var (v, w) in path)
            {
                var pivot = new IntegerCenter(cur, asCubes: true);
                foreach (var o in cur) IntegerOps.RotateAsCenters(o, pivot, v, w);
            }
            return cur;
        }

        [Test]
        public void SingleTesseract_AlreadyOptimal()
        {
            var piece = new Piece(new int[][] { new int[] { 0, 0, 0, 0 } });
            var res = PieceOrientation.BestViewRotation(piece.origins, new Camera4dParallel());
            // The front-facing facets of one tesseract tile the cavalier shadow without overlap.
            Assert.That(res.baselineOverlapPairs, Is.EqualTo(0));
            Assert.That(res.overlapPairs, Is.EqualTo(0));
            Assert.That(res.rotationPath, Is.Empty);
        }

        [Test]
        public void Result_NeverWorseThanBaseline()
        {
            // An L-shaped tetromino-like piece in 4D.
            var piece = new Piece(new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 },
                new int[] { 2,0,0,0 }, new int[] { 2,1,0,0 },
            });
            var res = PieceOrientation.BestViewRotation(piece.origins, new Camera4dParallel());
            Assert.That(res.overlapPairs, Is.LessThanOrEqualTo(res.baselineOverlapPairs));
        }

        [Test]
        public void ReportedCount_MatchesReturnedOrigins()
        {
            var piece = new Piece(new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 },
                new int[] { 0,1,0,0 }, new int[] { 0,0,1,0 },
            });
            var cam = new Camera4dParallel();
            var res = PieceOrientation.BestViewRotation(piece.origins, cam);

            Assert.That(res.baselineOverlapPairs,
                Is.EqualTo(PieceOrientation.CountOverlappingFrontCellPairs(piece.origins, cam)));
            Assert.That(res.overlapPairs,
                Is.EqualTo(PieceOrientation.CountOverlappingFrontCellPairs(res.origins, cam)));
        }

        [Test]
        public void RotationPath_ReproducesReturnedOrigins()
        {
            var piece = new Piece(new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 1,1,0,0 },
            });
            var res = PieceOrientation.BestViewRotation(piece.origins, new Camera4dParallel());
            var applied = ApplyPath(piece.origins, res.rotationPath);
            // Equal up to translation (the piece pivots about its own centroid each step).
            Assert.That(IntegerOps.MotionEqual(applied, res.origins), Is.True,
                "applying rotationPath must reproduce the returned orientation");
        }

        [Test]
        public void DefaultGameProjection_MatchesCamera4dParallel()
        {
            // The Geometry-free core hard-codes the cavalier constants of the game's default camera.
            // Lock them against drift: DefaultGame must reproduce new Camera4dParallel() exactly.
            var cam = new Camera4dParallel();
            var def = PieceProjectionOverlap.ParallelProjection.DefaultGame;
            for (int i = 0; i < 3; i++)
                for (int k = 0; k < 4; k++)
                    Assert.That(def.v[i][k], Is.EqualTo(cam.v[i].x[k]).Within(1e-12),
                        $"projection basis v[{i}][{k}] disagrees with new Camera4dParallel()");
            for (int k = 0; k < 4; k++)
                Assert.That(def.viewNormal[k], Is.EqualTo(cam.viewNormal.x[k]).Within(1e-12),
                    $"viewNormal[{k}] disagrees with new Camera4dParallel()");
        }

        [Test]
        public void OverlapCount_IsTranslationInvariant()
        {
            var cam = new Camera4dParallel();
            var origins = new int[][] {
                new int[] { 0,0,0,0 }, new int[] { 1,0,0,0 }, new int[] { 0,1,0,0 },
            };
            var shifted = IntegerOps.Clone(origins);
            foreach (var o in shifted) { o[0] += 3; o[2] -= 5; }
            Assert.That(PieceOrientation.CountOverlappingFrontCellPairs(origins, cam),
                Is.EqualTo(PieceOrientation.CountOverlappingFrontCellPairs(shifted, cam)));
        }
    }
}
