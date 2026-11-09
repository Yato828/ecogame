using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wattshift.Editor
{
    public static class GateFourInstaller
    {
        const string Folder="Assets/_WATTSHIFT/Data/CampaignG4/";
        static readonly string[] Names={"01-evening-peak","02-dark-streets","03-energy-of-life"};
        [MenuItem("WATTSHIFT/Install G4 UX and rating")]
        public static void Install()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(CitySliceBuilder.ScenePath);
            var session=UnityEngine.Object.FindFirstObjectByType<GameSession>();
            session.LegacyMissions=Names.Select(n=>AssetDatabase.LoadAssetAtPath<MissionDefinition>("Assets/_WATTSHIFT/Data/Campaign/"+n+".asset")).ToArray();
            session.Missions=session.LegacyMissions.Select((m,index)=>CloneMission(m,index)).ToArray();session.Mission=session.Missions[0];
            if(session.GetComponent<ProblemMarkers>()==null)session.gameObject.AddComponent<ProblemMarkers>();
            if(session.GetComponent<BuildDiagnostics>()==null)session.gameObject.AddComponent<BuildDiagnostics>();
            EditorUtility.SetDirty(session);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("WATTSHIFT_G4_INSTALLED: additive adapters; source art and transforms preserved");
        }
        static MissionDefinition CloneMission(MissionDefinition original,int index)
        {
            var m=Clone(original,Names[index]);m.Id=original.Id.Replace("-v3","-v4");m.UseDecisionRating=true;
            m.MinimumObservationHours=0;m.InvestigationAvailableHour=17;m.TargetSavingPercent=index==0?38:index==1?6:27;
            m.RationalSpend=index==0?440:index==1?380:550;
            m.Description=index==0?"В пустых коридорах школы горит свет, компьютеры оставлены включёнными.":index==1?"Старые фонари расходуют лишнюю энергию. Сохрани свет переходов и дорожек.":"Убери лишнюю нагрузку поликлиники, сохрани медицинские системы.";
            m.Problems=index==0?new[]{Problem("school-light","school","В пустых коридорах горит свет","lights"),Problem("school-pc","school","Неиспользуемые компьютеры работают","computers")}:index==1?new[]{Problem("crossings-light","crossings","Неэффективный свет переходов","street",true),Problem("path-light","walkways","Неэффективный свет дорожек","street")}:new[]{Problem("clinic-aux","clinic","Свет пустующих помещений","aux-lights"),Problem("clinic-admin","clinic","Неиспользуемая офисная техника","admin"),Problem("clinic-task","clinic","Неэффективный рабочий свет","task-lights")};
            m.Buildings=original.Buildings.Select(b=>
            {
                var copy=Clone(b,index+"-"+b.Id);
                copy.Upgrades=b.Upgrades.Select(u=>
                {
                    var offer=Clone(u,index+"-"+b.Id+"-"+u.Id);
                    if(offer.RequiredAfterHour<20)offer.RequiredAfterHour=17;
                    if(index==0 || (index==2&&b.Id=="clinic"))
                        foreach(var effect in offer.Effects)if(effect.OverrideSchedule)effect.EndHour=17;
                    offer.Description=offer.Description.Replace("18:00","17:00");EditorUtility.SetDirty(offer);return offer;
                }).ToArray();
                copy.Description=copy.Description.Replace("18:00","17:00");EditorUtility.SetDirty(copy);return copy;
            }).ToArray();
            m.Events=original.Events.Select(e=>new MissionEventDefinition{Id=e.Id,Hour=e.Hour,Text=e.Text}).ToArray();
            foreach(var e in m.Events)if(e.Id=="after-class"||e.Id=="aux-closed")e.Hour=17;
            EditorUtility.SetDirty(m);return m;
        }
        static MissionProblemDefinition Problem(string id,string building,string title,string device,bool urgent=false)=>new MissionProblemDefinition{Id=id,BuildingId=building,Title=title,DeviceIds=new[]{device},Required=true,Urgent=urgent,MinimumReductionPercent=25};
        static T Clone<T>(T source,string name)where T:ScriptableObject
        {
            string path=Folder+name+".asset";var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}
            EditorUtility.CopySerialized(source,asset);asset.name=name;return asset;
        }
    }
}
