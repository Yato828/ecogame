using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wattshift.Editor
{
    /// <summary>Data/adapters only: never rebuilds the approved district.</summary>
    public static class GateThreeInstaller
    {
        const string Folder = "Assets/_WATTSHIFT/Data/Campaign/";

        [MenuItem("WATTSHIFT/Install G3-A first shift")]
        public static void InstallA()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(CitySliceBuilder.ScenePath);
            var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            MissionDefinition first = FirstShift();
            session.Mission = first; session.Missions = new[] { first };
            var atmosphere = UnityEngine.Object.FindFirstObjectByType<CityAtmosphere>();
            if (atmosphere == null) atmosphere = new GameObject("Game-clock atmosphere").AddComponent<CityAtmosphere>();
            atmosphere.Sun = RenderSettings.sun; atmosphere.SceneCamera = Camera.main; session.Atmosphere = atmosphere;
            atmosphere.Streetlights = InstallEveningLights();
            EditorUtility.SetDirty(session); EditorUtility.SetDirty(atmosphere);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("WATTSHIFT_G3_A_INSTALLED: first shift; approved art and placements retained");
        }

        static StreetlightView[] InstallEveningLights()
        {
            var lamps = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.parent != null && t.parent.name.StartsWith("KENNEY", StringComparison.Ordinal) && t.name.EndsWith("light-curved", StringComparison.Ordinal)).OrderBy(t=>t.position.z).ThenBy(t=>t.position.x).ToArray();
            if(lamps.Length!=4)throw new InvalidOperationException("Expected four approved genuine streetlight models, found "+lamps.Length);
            return lamps.Select((t,index)=>
            {
                var view=t.GetComponent<StreetlightView>();if(view==null)view=t.gameObject.AddComponent<StreetlightView>();
                view.BuildingId="street-baseline-"+index;
                Transform fixture=t.Find("Evening luminaire");
                if(fixture==null){fixture=new GameObject("Evening luminaire").transform;fixture.SetParent(t,false);}
                Bounds bounds=t.GetComponentsInChildren<MeshRenderer>()[0].bounds;
                fixture.position=bounds.center+Vector3.up*bounds.size.y*.35f;
                fixture.rotation=Quaternion.Euler(90,0,0);
                var light=fixture.GetComponent<Light>();if(light==null)light=fixture.gameObject.AddComponent<Light>();
                light.type=LightType.Spot;light.spotAngle=95;light.innerSpotAngle=45;light.range=4;light.color=new Color(1,.82f,.55f);light.intensity=2.2f;light.shadows=LightShadows.None;light.enabled=false;
                view.BaseIntensity=2.2f;view.Lights=new[]{light};EditorUtility.SetDirty(view);return view;
            }).ToArray();
        }

        static MissionDefinition FirstShift()
        {
            var buildings = BaseBuildings("peak");
            var school = buildings.Single(b => b.Id == "school");
            foreach(var device in school.Devices)if(device.Id=="lights"||device.Id=="computers")device.Priority=LoadPriority.NonCritical;
            school.Upgrades = new[] {
                Upgrade("peak-lights-schedule", "school-lights", "Расписание пустых коридоров", "После ухода людей в 18:00 свет пустых коридоров выключается. Охрана сохраняет питание.", 180, Schedule("lights",8,18), "school-lighting"),
                Upgrade("peak-lights-sensors", "school-sensors", "Датчики присутствия", "Для редко посещаемых помещений: учебный коэффициент занятости 35%; свет включается при необходимости.", 220, Effect("lights",.35f), "school-lighting"),
                Upgrade("peak-lights-led", "school-led", "Эффективные светильники", "В учебной модели та же освещённость при мощности 40% от исходной. Расписание сохраняется.", 140, Effect("lights",.4f), "school-lighting"),
                Upgrade("peak-pc", "school-computers", "Сон неиспользуемых компьютеров", "После завершения занятий компьютеры переходят в отключённое состояние, а критические системы не затрагиваются.",260, Schedule("computers",8,18)),
                Upgrade("peak-unsafe", "school-dark", "Отключить свет во всех помещениях", "Нарушит обязательный комфорт. Система не позволит применить решение.",70, Effect("lights",0), "",-40),
                Upgrade("peak-expensive", "school-expensive", "Полная замена компьютеров", "Дорогостоящая альтернатива: сначала сопоставьте расходы и необходимый результат.",1200,Effect("computers",.5f)) };
            foreach (var upgrade in school.Upgrades) { upgrade.RequiredAfterHour = 18; EditorUtility.SetDirty(upgrade); }
            var mission = Get<MissionDefinition>("01-evening-peak");
            Configure(mission,"evening-peak-v3","Вечерний пик","После занятий найдите избыточную нагрузку школы. Выберите подходящие меры и проверьте их реальный эффект за смену.",buildings.Concat(StreetDefinitions("peak",false,false)).ToArray(),650,24,new[]{"school"},18,new[]{24f,26f,30f});
            mission.Events = new[] {
                Event("start",17,"Начало смены. Осмотрите район и запомните важные службы."),
                Event("after-class",18,"Занятия закончились, но спрос школы не снизился. Обследуйте источник сканером."),
                Event("observe",20,"Вечерний пик: сравните работу школы до и после вмешательства."),
                Event("review",22,"До конца смены час. Позднее решение уже не отменит израсходованную энергию."),
                Event("end",23,"Смена закончилась. Подведите итоги по фактической энергии и безопасности.") };
            EditorUtility.SetDirty(school); EditorUtility.SetDirty(mission); return mission;
        }

        static BuildingDefinition[] BaseBuildings(string prefix)
        {
            return new[] { "home-a","home-b","home-c","home-d","home-e","home-f","school","clinic" }.Select(id =>
            {
                var original = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_WATTSHIFT/Data/Mission1/"+id+".asset");
                var copy = Get<BuildingDefinition>(prefix+"-"+id);
                copy.Id=id; copy.Title=original.Title; copy.BaseComfort=100; copy.Upgrades=Array.Empty<UpgradeDefinition>();
                copy.Description=id=="school"?"Занятия заканчиваются в 18:00. Отделите оставленную технику от необходимого охранного питания.":id=="clinic"?"Питание медицинских систем должно сохраняться; другие нагрузки имеют разные приоритеты.":"Учебный жилой дом: непрерывная работа холодильника и вечернее освещение учитываются отдельно.";
                copy.Devices=original.Devices.Select(d=> new DeviceDefinition { Id=d.Id,Title=DeviceTitle(d.Id),PowerKW=d.PowerKW,StartHour=d.StartHour,EndHour=d.EndHour,Essential=d.Essential,IsLighting=d.IsLighting,Priority=d.Essential?LoadPriority.Critical:d.IsLighting?LoadPriority.Important:LoadPriority.NonCritical }).ToArray();
                EditorUtility.SetDirty(copy); return copy;
            }).ToArray();
        }

        [MenuItem("WATTSHIFT/Install G3 full campaign")]
        public static void InstallFull()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();EditorSceneManager.OpenScene(CitySliceBuilder.ScenePath);
            var session=UnityEngine.Object.FindFirstObjectByType<GameSession>();
            session.Missions=new[]{FirstShift(),StreetShift(),ClinicShift()};session.Mission=session.Missions[0];
            var atmosphere=UnityEngine.Object.FindFirstObjectByType<CityAtmosphere>();
            if(atmosphere==null)atmosphere=new GameObject("Game-clock atmosphere").AddComponent<CityAtmosphere>();
            atmosphere.Sun=RenderSettings.sun;atmosphere.SceneCamera=Camera.main;session.Atmosphere=atmosphere;
            atmosphere.Streetlights=InstallEveningLights();
            for(int i=0;i<atmosphere.Streetlights.Length;i++)
            {
                StreetlightView lamp=atmosphere.Streetlights[i];bool crossing=lamp.transform.position.x>0;
                lamp.BuildingId=crossing?"crossings":"walkways";
                var view=lamp.GetComponent<CityBuilding>();if(view==null)view=lamp.gameObject.AddComponent<CityBuilding>();
                view.SimulationId=lamp.BuildingId;view.Title=crossing?(lamp.transform.position.z>0?"Северный переход":"Южный переход"):(lamp.transform.position.z>0?"Дворовые дорожки":"Дорожки сквера");
                view.Category=crossing?"ОСВЕЩЕНИЕ ПЕРЕХОДОВ":"ПЕШЕХОДНЫЕ МАРШРУТЫ";view.Essential=crossing;
                view.Description="Управление относится к двум настоящим фонарям этой группы. Освещённость и мощность — разные параметры.";
                var collider=lamp.GetComponent<BoxCollider>();if(collider==null)collider=lamp.gameObject.AddComponent<BoxCollider>();
                collider.center=new Vector3(0,.34f,0);collider.size=new Vector3(.5f,.8f,.5f);
                EditorUtility.SetDirty(view);EditorUtility.SetDirty(lamp);
            }
            if(session.GetComponent<Wattshift.Presentation.CampaignAudio>()==null)session.gameObject.AddComponent<Wattshift.Presentation.CampaignAudio>();
            if(session.GetComponent<Wattshift.Presentation.QualityController>()==null)session.gameObject.AddComponent<Wattshift.Presentation.QualityController>();
            EditorUtility.SetDirty(session);EditorUtility.SetDirty(atmosphere);AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("WATTSHIFT_G3_B_INSTALLED:3 standalone educational shifts;2 source streetlight groups;no new buildings");
        }

        static MissionDefinition StreetShift()
        {
            var buildings=BaseBuildings("streets");OptimizeSchool(buildings);
            var mission=Get<MissionDefinition>("02-dark-streets");
            Configure(mission,"dark-streets-v3","Тёмные улицы","Обследуйте две группы фонарей. Снижайте мощность, сохраняя освещение переходов и безопасных дорожек.",buildings.Concat(StreetDefinitions("streets",false,true)).ToArray(),380,4,new[]{"crossings","walkways"},18,new[]{4f,5f,5.5f});
            mission.BaseReward=120;
            mission.Events=new[]{Event("start",17,"Скоро стемнеет. Найдите переходы и пешеходные маршруты."),Event("lights-on",18,"Фонари включились: исследуйте группы сканером и сравните их приоритеты."),Event("quiet-paths",20,"На дорожках снизилась активность: регулировка допустима здесь, но не на переходах."),Event("safety-check",22,"Проверьте, что свет переходов сохраняется, а дорожки не погасли."),Event("end",23,"Смена закончилась. Сравните экономию с сохранённой безопасностью.")};
            EditorUtility.SetDirty(mission);return mission;
        }

        static MissionDefinition ClinicShift()
        {
            var buildings=BaseBuildings("life");OptimizeSchool(buildings);
            var clinic=buildings.Single(b=>b.Id=="clinic");
            clinic.Description="Критические медицинские нагрузки питаются непрерывно. Рабочие зоны сохраняют освещение; пустующие вспомогательные помещения можно обследовать отдельно.";
            clinic.Devices=new[]{Device("medical","Критические медицинские системы",8,0,24,LoadPriority.Critical,true),Device("task-lights","Освещение рабочих зон",2,0,24,LoadPriority.Important,false,true,.8f),Device("aux-lights","Свет вспомогательных помещений",2,8,23,LoadPriority.NonCritical,false,true),Device("admin","Неиспользуемая административная техника",3,8,23,LoadPriority.NonCritical),Device("vent","Рабочая вентиляция",1,0,24,LoadPriority.Important,false,false,1)};
            clinic.Upgrades=new[]{
                Upgrade("life-aux","clinic-aux","Расписание пустующих помещений","В учебном сценарии эти помещения пустуют после 18:00; рабочие и критические системы не затрагиваются.",150,Schedule("aux-lights",8,18),"clinic-auxiliary"),
                Upgrade("life-sensors","clinic-sensors","Датчики вспомогательного света","Для непостоянно используемых помещений: коэффициент занятости 35%. Свет доступен при необходимости.",200,Effect("aux-lights",.35f),"clinic-auxiliary"),
                Upgrade("life-admin","clinic-admin","Сон административной техники","После 18:00 отключается только неиспользуемая офисная нагрузка, не медицинское оборудование.",180,Schedule("admin",8,18)),
                Upgrade("life-led","clinic-led","Эффективный свет рабочих зон","Сохраняет учебный уровень освещённости 100% при меньшей мощности.",220,Effect("task-lights",.5f)),
                Upgrade("life-unsafe","clinic-off","Отключить медицинские системы","Недопустимо: непрерывное критическое питание защищено и не может быть предметом экономии.",0,Effect("medical",0,0)),
                Upgrade("life-task-off","clinic-task-off","Выключить свет рабочих зон","Нарушает минимальный уровень работы важных помещений; решение будет отклонено.",0,Effect("task-lights",0,0))};
            foreach(var offer in clinic.Upgrades){offer.RequiredAfterHour=18;EditorUtility.SetDirty(offer);}
            var mission=Get<MissionDefinition>("03-energy-of-life");
            Configure(mission,"energy-of-life-v3","Энергия жизни","Разделите нагрузки поликлиники по приоритету. Уберите избыточное потребление, сохраняя безопасную работу учреждения.",buildings.Concat(StreetDefinitions("life",true,false)).ToArray(),550,14,new[]{"clinic"},18,new[]{14f,17f,20f});
            mission.BaseReward=150;
            mission.Events=new[]{Event("start",17,"Поликлиника работает. Критические системы требуют непрерывного питания."),Event("aux-closed",18,"Часть вспомогательных помещений опустела. Обследуйте фактические источники нагрузки."),Event("priority-review",20,"Сравните критические, рабочие и некритичные нагрузки: уменьшение мощности не должно лишать объект нужной службы."),Event("final-review",22,"Оцените эффект установок. Не отключайте питание важных служб ради целевого процента."),Event("end",23,"Подведите итоги безопасной смены.")};
            EditorUtility.SetDirty(clinic);EditorUtility.SetDirty(mission);return mission;
        }

        static BuildingDefinition[] StreetDefinitions(string prefix,bool efficient,bool offers)
        {
            return new[]{"crossings","walkways"}.Select(id=>
            {
                bool crossing=id=="crossings";var b=Get<BuildingDefinition>(prefix+"-"+id);
                b.Id=id;b.Title=crossing?"Освещение переходов":"Освещение пешеходных дорожек";b.BaseComfort=100;
                b.Description=crossing?"Группа из двух фонарей у переходов. Минимальный учебный уровень освещения 80%: уменьшать необходимую видимость нельзя.":"Группа из двух фонарей дорожек. Минимальный учебный уровень 45%; после 20:00 допустимо обоснованное регулирование.";
                b.Devices=new[]{Device("street","Два фонаря группы",efficient ? .27f : .9f,18,24,crossing?LoadPriority.Critical:LoadPriority.Important,false,true,crossing ? .8f : .45f)};
                b.Upgrades=Array.Empty<UpgradeDefinition>();
                if(offers)
                {
                    var led=Upgrade(prefix+"-"+id+"-led",id+"-led","Светодиодные светильники","Та же освещённость при учебной мощности 30% от исходной. Меняются оба фонаря группы.",crossing?220:160,Effect("street",.3f),"light-source");
                    var off=Upgrade(prefix+"-"+id+"-off",id+"-off","Полностью выключить группу","Недопустимо: необходимый свет переходов и дорожек должен сохраняться.",0,Effect("street",0,0),"light-source");
                    if(crossing)b.Upgrades=new[]{led,off};
                    else
                    {
                        var adaptive=Upgrade(prefix+"-walkways-adaptive","walkways-adaptive","Регулировка малолюдных дорожек","Доступно после 20:00: мощность 35%, учебный уровень освещения 65%, выше минимального уровня 45%.",110,Effect("street",.35f,.65f),"light-source");
                        adaptive.RequiredAfterHour=20;EditorUtility.SetDirty(adaptive);b.Upgrades=new[]{led,adaptive,off};
                    }
                    led.RequiredAfterHour=off.RequiredAfterHour=18;EditorUtility.SetDirty(led);EditorUtility.SetDirty(off);
                }
                EditorUtility.SetDirty(b);return b;
            }).ToArray();
        }
        static void OptimizeSchool(BuildingDefinition[] buildings)
        {
            var school=buildings.Single(b=>b.Id=="school");
            foreach(var d in school.Devices)if(d.Id=="lights"||d.Id=="computers")d.EndHour=18;
            EditorUtility.SetDirty(school);
        }
        static DeviceDefinition Device(string id,string title,float power,float start,float end,LoadPriority priority,bool essential=false,bool lighting=false,float minimum=0)=>new DeviceDefinition{Id=id,Title=title,PowerKW=power,StartHour=start,EndHour=end,Priority=priority,Essential=essential,IsLighting=lighting,MinimumService=minimum};
        static string DeviceTitle(string id)=>id=="fridge"?"Холодильник":id=="standby"?"Дежурные бытовые устройства":id=="lights"?"Освещение":id=="security"?"Охранные системы":id=="computers"?"Компьютеры":id=="medical"?"Критические медицинские системы":"Рабочее оборудование";
        static void Configure(MissionDefinition mission,string id,string title,string description,BuildingDefinition[] buildings,int budget,float target,string[] required,float investigation,float[] thresholds)
        {
            mission.Id=id;mission.Title=title;mission.Description=description;
            mission.Assumptions="Учебные примерные нагрузки, расходы в условных единицах; расписания и коэффициенты не являются рекомендацией для реальных объектов.";
            mission.StartHour=17;mission.EndHour=23;mission.EvaluationHour=20;mission.UseTimeline=true;mission.RealDurationSeconds=360;
            mission.InitialBudget=budget;mission.TargetSavingPercent=target;mission.MinComfort=80;mission.RequireEndOfShift=true;
            mission.MinimumUpgradeCount=2;mission.MinimumObservationHours=.5f;mission.RequiredBuildingIds=required;mission.InvestigationAvailableHour=investigation;
            mission.StarSavingThresholds=thresholds;mission.BaseReward=100;mission.Buildings=buildings;
        }
        static MissionEventDefinition Event(string id,float hour,string text)=>new MissionEventDefinition{Id=id,Hour=hour,Text=text};
        static UpgradeEffect Effect(string id,float power,float service=1)=>new UpgradeEffect{DeviceId=id,PowerMultiplier=power,ServiceMultiplier=service};
        static UpgradeEffect Schedule(string id,float start,float end)=>new UpgradeEffect{DeviceId=id,PowerMultiplier=1,ServiceMultiplier=1,OverrideSchedule=true,StartHour=start,EndHour=end};
        static UpgradeDefinition Upgrade(string file,string id,string title,string description,int cost,UpgradeEffect effect,string group="",float comfort=0)
        {
            var asset=Get<UpgradeDefinition>(file);asset.Id=id;asset.Title=title;asset.Description=description;asset.Cost=cost;asset.ComfortDelta=comfort;
            asset.Effects=new[]{effect};asset.ExclusiveGroup=group;EditorUtility.SetDirty(asset);return asset;
        }
        static T Get<T>(string name)where T:ScriptableObject
        {
            string path=Folder+name+".asset";var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset;
        }
    }
}
