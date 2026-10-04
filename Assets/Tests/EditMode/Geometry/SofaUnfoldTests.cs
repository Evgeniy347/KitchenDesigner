using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Раскладывание дивана-книжки: два этапа СТРОГО ПО ОЧЕРЕДИ. Этап 1
    /// (фото «сложен» → «сиденье выдвинуто»): сиденье уезжает вперёд и открывает
    /// короб, спинка стоит. Этап 2 («короб виден» → «кровать»): спинка заваливается
    /// вперёд на петле по верхней задней кромке короба и ложится на него плашмя
    /// вровень с сиденьем.
    ///
    /// Всё здесь — арифметика в миллиметрах без сцены, поэтому живёт на быстром
    /// пути. Поворот спинки проверяется по ФОРМУЛЕ поворота точек вокруг петли
    /// (<see cref="SofaUnfold.BackrestCentreMM"/>), а честное соответствие этой
    /// формулы тому, что рисует Unity, закреплено в EditMode-тесте на сцене:
    /// Quaternion.Euler под dotnet недоступен.</summary>
    public class SofaUnfoldTests
    {
        private const int Seat = SofaLayout.DefaultSeatHeightMM;
        private const float Eps = 1e-3f;

        private static Vector3Int Default() => new Vector3Int(
            SofaLayout.DefaultWidthMM, SofaLayout.DefaultHeightMM, SofaLayout.DefaultDepthMM);

        private static float Travel(int seatHeight = Seat)
            => SofaUnfold.SeatSlideTravelMM(SofaLayout.DefaultDepthMM, seatHeight);

        [Test]
        public void Next_StepsFoldedExtendedBedFolded_AndWrapsAround()
        {
            Assert.AreEqual(SofaStage.Extended, SofaUnfold.Next(SofaStage.Folded),
                "первое нажатие выдвигает сиденье");
            Assert.AreEqual(SofaStage.Bed, SofaUnfold.Next(SofaStage.Extended),
                "второе раскладывает кровать");
            Assert.AreEqual(SofaStage.Folded, SofaUnfold.Next(SofaStage.Bed),
                "третье складывает обратно: цикл замкнут, иначе кнопка в панели застряла бы");
        }

        [Test]
        public void StageFrom_ClampsANumberFromACorruptedSave_ToTheNearestStage()
        {
            Assert.AreEqual(SofaStage.Folded, SofaUnfold.StageFrom(-3),
                "отрицательный этап из испорченного сохранения — это сложенный диван");
            Assert.AreEqual(SofaStage.Bed, SofaUnfold.StageFrom(77),
                "а число за пределами — кровать: диван не должен уехать за свою кинематику");
            Assert.AreEqual(SofaStage.Extended, SofaUnfold.StageFrom(1),
                "допустимое число проходит как есть");
        }

        [Test]
        public void Ease_IsSlowAtBothEnds_AndHalfwayAtTheMiddle()
        {
            Assert.AreEqual(0f, SofaUnfold.Ease(0f), Eps, "начало этапа");
            Assert.AreEqual(1f, SofaUnfold.Ease(1f), Eps, "конец этапа");
            Assert.AreEqual(0.5f, SofaUnfold.Ease(0.5f), Eps, "середина ровно посередине");
            Assert.Less(SofaUnfold.Ease(0.1f), 0.1f,
                "разгон плавный, как у ящика: линейный старт читался бы рывком");
            Assert.AreEqual(1f, SofaUnfold.Ease(3f), Eps,
                "за пределом этапа плавность не выходит за единицу");
            Assert.AreEqual(0f, SofaUnfold.Ease(-3f), Eps, "и не уходит под ноль");
        }

        [Test]
        public void SeatTravel_IsTheBoxDepthPlusTheClearance_AtTheDefaultSeatHeight()
        {
            Assert.AreEqual(618f, SofaBoxLayout.DepthMM(SofaLayout.DefaultDepthMM), Eps,
                "короб короче сиденья на переднюю губу 100 мм и на 2 мм зазора от петли: 720 − 100 − 2");
            Assert.AreEqual(640f, Travel(), Eps,
                "сиденье выезжает на глубину короба плюс зазор 20 мм — ровно настолько, "
                + "чтобы открыть короб целиком");
        }

        [Test]
        public void PoseAt_Folded_IsTheRestingPose()
        {
            var pose = SofaUnfold.PoseAt(0f, SofaLayout.DefaultDepthMM, Seat);

            Assert.AreEqual(0f, pose.SeatSlideMM, Eps, "сиденье на месте");
            Assert.AreEqual(0f, pose.BackrestAngleDeg, Eps, "спинка стоит");
        }
        [Test]
        public void PoseAt_Extended_HasTheSeatPulledOutFurtherThanTheBed_AndTheBackrestStillStanding()
        {
            var pose = SofaUnfold.PoseAt(1f, SofaLayout.DefaultDepthMM, Seat);

            Assert.AreEqual(Travel() + SofaUnfold.ExtraPullMM, pose.SeatSlideMM, Eps,
                "на этапе 1 сиденье выдвинуто с запасом на доступ к коробу: фото 43 — между "
                + "сиденьем и коробом зазор шире, чем у готовой кровати");
            Assert.AreEqual(0f, pose.BackrestAngleDeg, Eps,
                "спинка к этому моменту ещё не тронулась: фото 43 — спинка стоит");
            Assert.IsFalse(pose.CushionsOnSeat,
                "подушки сняты: на фото 43 на сиденье их нет, и они закрыли бы короб");
        }

        [Test]
        public void PoseAt_Bed_HasTheSeatBackAtTheBedGap_AndTheBackrestFlat()
        {
            var pose = SofaUnfold.PoseAt(2f, SofaLayout.DefaultDepthMM, Seat);

            Assert.AreEqual(Travel(), pose.SeatSlideMM, Eps,
                "сиденье подъехало обратно и стоит впритык к лежащей спинке (зазор 20 мм)");
            Assert.AreEqual(90f, pose.BackrestAngleDeg, Eps,
                "спинка повёрнута на 90 градусов — лежит плашмя");
            Assert.IsFalse(pose.CushionsOnSeat, "подушек на кровати нет, как на фото 40");
        }

        [Test]
        public void PoseAt_Folded_HasTheCushionsOnTheSeat_AndAnyMovementTakesThemOff()
        {
            Assert.IsTrue(SofaUnfold.PoseAt(0f, SofaLayout.DefaultDepthMM, Seat).CushionsOnSeat,
                "сложенный диван с подушками");
            Assert.IsFalse(SofaUnfold.PoseAt(0.01f, SofaLayout.DefaultDepthMM, Seat).CushionsOnSeat,
                "стоит сиденью тронуться — подушки уже сняты: иначе они ехали бы по ходу "
                + "и закрывали короб");
        }

        [Test]
        public void TheTwoStages_AreStrictlySequential_TheBackrestWaitsForTheFullPull()
        {
            int checkedSteps = 0;
            for (float progress = 0f; progress <= 2f + Eps; progress += 0.01f)
            {
                var pose = SofaUnfold.PoseAt(progress, SofaLayout.DefaultDepthMM, Seat);
                checkedSteps++;

                if (progress <= 1f)
                    Assert.AreEqual(0f, pose.BackrestAngleDeg, Eps,
                        "пока сиденье выезжает (этап 1), спинка не шевелится: этапы не "
                        + "перекрываются, progress=" + progress);
                if (progress >= 1f)
                    Assert.LessOrEqual(pose.SeatSlideMM, Travel() + SofaUnfold.ExtraPullMM + Eps,
                        "на этапе 2 сиденье только возвращается из выдвинутого положения, "
                        + "progress=" + progress);
            }

            Assert.GreaterOrEqual(checkedSteps, 200,
                "обход прогресса не сделал и двухсот шагов — тест позеленел бы, ничего не "
                + "проверив");
        }

        [Test]
        public void TheSeat_GoesOutDuringStageOne_AndComesBackDuringStageTwo_WhileTheBackrestOnlyTurnsForward()
        {
            float lastSlide = 0f;
            float lastAngle = 0f;
            for (float progress = 0f; progress <= 2f + Eps; progress += 0.01f)
            {
                var pose = SofaUnfold.PoseAt(progress, SofaLayout.DefaultDepthMM, Seat);
                if (progress <= 1f)
                    Assert.GreaterOrEqual(pose.SeatSlideMM, lastSlide - Eps,
                        "на этапе 1 сиденье не откатывается назад");
                else
                    Assert.LessOrEqual(pose.SeatSlideMM, lastSlide + Eps,
                        "на этапе 2 сиденье только возвращается");
                Assert.GreaterOrEqual(pose.BackrestAngleDeg, lastAngle - Eps,
                    "спинка не поднимается обратно");
                lastSlide = pose.SeatSlideMM;
                lastAngle = pose.BackrestAngleDeg;
            }
        }

        private static (float minZ, float maxZ) PanelZRangeMM(Vector3Int dims, int seat,
            float angleDeg, float shiftMM)
        {
            float lower = SofaUnfold.BelowHingeMM(seat);
            float t = SofaLayout.BackrestThicknessMM;
            float radians = angleDeg * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            float hingeZ = SofaUnfold.HingeMM(dims, seat).z;
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (float dy in new[] { -lower, SofaLayout.BackrestHeightMM - lower })
            foreach (float dz in new[] { 0f, -t })
            {
                float z = hingeZ + dy * sin + dz * cos + shiftMM;
                min = Mathf.Min(min, z);
                max = Mathf.Max(max, z);
            }

            return (min, max);
        }

        [Test]
        public void Backrest_WhileFolding_NeverCrossesTheBackWallPlane_AtAnyAngleAndAnySeatHeight()
        {
            int checkedPoses = 0;
            for (int seat = SofaLayout.MinSeatHeightMM; seat <= SofaLayout.MaxSeatHeightMM; seat += 10)
            {
                var dims = Default();
                float wall = -dims.z * 0.5f;
                for (float angle = 0f; angle <= 90f + Eps; angle += 0.5f)
                {
                    var (minZ, _) = PanelZRangeMM(dims, seat, angle,
                        SofaUnfold.BackrestShiftMM(seat, angle));
                    checkedPoses++;

                    Assert.GreaterOrEqual(minZ, wall - Eps,
                        "спинка не должна заходить за плоскость задней стенки ни на миллиметр: "
                        + "диван стоит вплотную к стене. Высота сиденья " + seat
                        + ", угол " + angle);
                }
            }

            Assert.GreaterOrEqual(checkedPoses, 2500,
                "перебор не дошёл до двух с половиной тысяч поз — тест ничего не доказал бы");
        }

        [Test]
        public void TheWallSensor_ActuallyFiresWithoutTheShift_SoTheTestAboveProvesSomething()
        {
            var dims = Default();
            float deepest = 0f;
            for (float angle = 0f; angle <= 90f; angle += 1f)
            {
                var (minZ, _) = PanelZRangeMM(dims, Seat, angle, 0f);
                deepest = Mathf.Max(deepest, -dims.z * 0.5f - minZ);
            }

            Assert.Greater(deepest, 5f,
                "без поправки спинка при повороте заходит за стену на 17 мм: если сенсор этого "
                + "не видит, зелёный тест выше — пустышка");
        }

        [Test]
        public void TheShift_IsZeroAtBothEndsOfTheTurn_SoTheLyingBackrestIsWhereItWas()
        {
            Assert.AreEqual(0f, SofaUnfold.BackrestShiftMM(Seat, 0f), Eps,
                "стоящая спинка не смещается");
            Assert.AreEqual(0f, SofaUnfold.BackrestShiftMM(Seat, 90f), Eps,
                "и лежащая тоже: поправка нужна только по дороге");
            Assert.Greater(SofaUnfold.BackrestShiftMM(Seat, 24f), 10f,
                "а на середине поворота это ощутимые миллиметры");
        }

        [Test]
        public void SlidSeat_NeverTouchesTheTurningBackrest_AtAnyProgress()
        {
            var dims = Default();
            int checkedSteps = 0;
            for (int seat = SofaLayout.MinSeatHeightMM; seat <= SofaLayout.MaxSeatHeightMM; seat += 20)
            for (float progress = 0.02f; progress <= 2f + Eps; progress += 0.02f)
            {
                var pose = SofaUnfold.PoseAt(progress, dims.z, seat);
                var (_, maxZ) = PanelZRangeMM(dims, seat, pose.BackrestAngleDeg,
                    pose.BackrestShiftMM);
                float seatRear = SofaLayout.BackrestFrontZMM(dims) + pose.SeatSlideMM;
                checkedSteps++;

                float needed = progress <= 1f ? 0f : SofaUnfold.SlideClearanceMM;
                Assert.GreaterOrEqual(seatRear - maxZ, needed - Eps,
                    "сиденье не должно упираться в поворачивающуюся спинку: высота сиденья "
                    + seat + ", прогресс " + progress);
            }

            Assert.GreaterOrEqual(checkedSteps, 500, "перебор слишком короткий");
        }


        [Test]
        public void Travel_LeavesTheClearanceBetweenTheSlidSeatAndTheBoxFront()
        {
            var dims = Default();
            var panels = SofaBoxLayout.Panels(dims, Seat, SofaLayout.DefaultCornerRadiusMM);
            float boxFront = float.MinValue;
            foreach (var panel in panels)
                boxFront = Mathf.Max(boxFront, panel.CentreMM.z + panel.SizeMM.z * 0.5f);

            float seatRearAfterSlide = SofaLayout.BackrestFrontZMM(dims) + Travel();

            Assert.AreEqual(SofaUnfold.SlideClearanceMM, seatRearAfterSlide - boxFront, Eps,
                "задняя кромка выдвинутого сиденья отстоит от передней стенки короба на "
                + "зазор: ближе — сиденье цеплялось бы за короб, дальше — торчала бы щель");
        }

        [Test]
        public void Hinge_SitsOnTheRearTopEdgeOfTheBox()
        {
            var dims = Default();
            var hinge = SofaUnfold.HingeMM(dims, Seat);
            var panels = SofaBoxLayout.Panels(dims, Seat, SofaLayout.DefaultCornerRadiusMM);

            float boxTop = float.MinValue;
            float boxRear = float.MaxValue;
            foreach (var panel in panels)
            {
                boxTop = Mathf.Max(boxTop, panel.CentreMM.y + panel.SizeMM.y * 0.5f);
                boxRear = Mathf.Min(boxRear, panel.CentreMM.z - panel.SizeMM.z * 0.5f);
            }

            Assert.AreEqual(boxTop, hinge.y, Eps,
                "петля на высоте верхней кромки короба — как на фото, где петли видны там, "
                + "где спинка встречает короб");
            Assert.AreEqual(boxRear, hinge.z + SofaBoxLayout.RearSetbackMM, Eps,
                "и на его задней кромке, прямо за которой стоит сложенная спинка: между петлёй "
                + "и задней стенкой короба 2 мм — иначе задняя грань короба лежала бы в одной "
                + "плоскости с задней гранью сиденья и мерцала бы (CoplanarSurfaceDetector)");
        }

        [Test]
        public void Backrest_RotatedByNothing_IsExactlyWhereTheLayoutPutsIt()
        {
            var dims = Default();

            Assert.AreEqual(SofaLayout.Backrest(dims).CentreMM,
                SofaUnfold.BackrestCentreMM(dims, Seat, 0f),
                "нулевой поворот не двигает спинку: формула поворота вокруг петли обязана "
                + "вернуть то, что даёт раскладка сложенного дивана");
        }

        [Test]
        public void Backrest_AtBed_LiesFlatOnTheBox_FlushWithTheSeatTop()
        {
            var dims = Default();
            var centre = SofaUnfold.BackrestCentreMM(dims, Seat, SofaUnfold.BackrestFlatAngleDeg);
            float seatTop = -dims.y * 0.5f + Seat;
            float boxTop = SofaBoxLayout.TopYMM(dims, Seat);

            Assert.AreEqual(seatTop, centre.y + SofaLayout.BackrestThicknessMM * 0.5f, Eps,
                "верх лежащей спинки вровень с верхом сиденья: фото «кровать» — обе половины "
                + "на одной высоте");
            Assert.AreEqual(boxTop, centre.y - SofaLayout.BackrestThicknessMM * 0.5f, Eps,
                "а низ лежащей спинки ровно на верхней кромке короба: она лежит НА коробе, "
                + "а не утоплена в него и не висит над ним");
        }

        [Test]
        public void Backrest_AtBed_CoversTheBoxFromTheWallSideToItsFront()
        {
            var dims = Default();
            var centre = SofaUnfold.BackrestCentreMM(dims, Seat, SofaUnfold.BackrestFlatAngleDeg);
            float far = centre.z + SofaLayout.BackrestHeightMM * 0.5f;
            float near = centre.z - SofaLayout.BackrestHeightMM * 0.5f;
            float boxFront = SofaLayout.BackrestFrontZMM(dims) + SofaBoxLayout.RearSetbackMM
                + SofaBoxLayout.DepthMM(dims.z);

            Assert.AreEqual(boxFront, far, Eps,
                "дальний край лежащей спинки совпадает с передней стенкой короба: крышка "
                + "накрывает короб ровно");
            Assert.GreaterOrEqual(near, -dims.z * 0.5f - Eps,
                "ближний край остаётся перед стеной: лёжа, спинка не уходит в неё");
        }

        [Test]
        public void Travel_AlsoClearsTheFlatBackrest_AtEverySeatHeight()
        {
            var dims = Default();
            int checkedHeights = 0;
            for (int seat = SofaLayout.MinSeatHeightMM; seat <= SofaLayout.MaxSeatHeightMM; seat += 10)
            {
                var centre = SofaUnfold.BackrestCentreMM(dims, seat,
                    SofaUnfold.BackrestFlatAngleDeg);
                float panelFar = centre.z + SofaLayout.BackrestHeightMM * 0.5f;
                float seatRear = SofaLayout.BackrestFrontZMM(dims)
                    + SofaUnfold.SeatSlideTravelMM(dims.z, seat);
                checkedHeights++;

                Assert.GreaterOrEqual(seatRear - panelFar, SofaUnfold.SlideClearanceMM - Eps,
                    "лежащая спинка не должна упираться в выдвинутое сиденье: чем ниже "
                    + "сиденье, тем дальше вперёд заходит спинка — выезд обязан это учесть. "
                    + "Высота сиденья " + seat);
            }

            Assert.GreaterOrEqual(checkedHeights, 15,
                "перебор высот сиденья не прошёл и пятнадцати значений");
        }

    }
}
