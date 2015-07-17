using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace Microsoft.TeamFoundation.SDK
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Connect to Team Foundation Server. The form of the url is http://server:port/vpath.
                //     Server - the name of the server that is running the application tier for Team Foundation.
                //     port - the port that Team Foundation uses. The default port is 8080.
                //     vpath - the virtual path to the Team Foundation application. The default path is tfs. sharath
                
                TfsConfigurationServer configurationServer =
                    TfsConfigurationServerFactory.GetConfigurationServer(new Uri("https://minacs.visualstudio.com"));

                // Get the catalog of team project collections
                CatalogNode catalogNode = configurationServer.CatalogNode;
                ReadOnlyCollection<CatalogNode> tpcNodes = catalogNode.QueryChildren(
                    new Guid[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);

                // Process each team project collection. Suresh GIT Comment in the title
                foreach (CatalogNode tpcNode in tpcNodes)
                {
                    // Use the InstanceId property to get the team project collection
                    Guid tpcId = new Guid(tpcNode.Resource.Properties["InstanceId"]);
                    TfsTeamProjectCollection tpc = configurationServer.GetTeamProjectCollection(tpcId);

                    // Get the work item store
                    WorkItemStore wiStore = tpc.GetService<WorkItemStore>();

                    /*
                    // FLAT LIST QUERY EXECUTION testing by raghu
//this is sahithi
                    Dictionary<string, string> values = new Dictionary<string, string>();
                    values.Add("project", "Aurora Reporting (New)");
                    values.Add("iterationpath", "Aurora Reporting (New)\\Sprint 18");

                    Query query = new Query(wiStore, 
                        "SELECT [System.Id] FROM WorkItems " +
                        "  WHERE [System.TeamProject] = @project " +
                        " and [System.IterationPath] UNDER @iterationpath"
                        , values);

                    Console.WriteLine(" Query...." + query.QueryString);
                    WorkItemCollection workItems = wiStore.Query(query.QueryString);
                    WorkItem workItem = workItems[0];
                    Console.WriteLine("Finally....." + workItems.Count.ToString() + " ---" +  workItem.IterationPath);
                    */
                    
                    // TREE LIST QUERY EXECUTION
                    Dictionary<string, string> values = new Dictionary<string, string>();
                    values.Add("project", "Aurora Reporting (New)");
                    values.Add("iterationpath", "Aurora Reporting (New)\\Sprint 18");

                    Query wiQuery = new Query(wiStore,
                        "SELECT [System.Id] FROM WorkItemLinks " +
                        "  WHERE [Source].[System.TeamProject] = @project " +
                        " and [Source].[System.IterationPath] UNDER @iterationpath" +
                        " and [System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward' " +
                        " and [Target].[System.WorkItemType] in ( 'User Story', 'Task','Bug')  "
                        , values);

                    Console.WriteLine(" Query...." + wiQuery.QueryString);
                    WorkItemLinkInfo[] wiTrees = wiQuery.RunLinkQuery();

                    // Print the trees of user stories, with the estimated sizes of each leaf node
                    PrintTrees(wiStore, wiTrees, "    ", 0, 0);
                    
                   
                    // Query for the trees of active user stories in the team project collection
                    /*
                    StringBuilder queryString = new StringBuilder("SELECT [System.Id] FROM WorkItemLinks WHERE ");
                    queryString.Append("([Source].[System.WorkItemType] = 'User Story' AND [Source].[System.State] = 'Active') AND ");
                    queryString.Append("([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward') And ");
                    queryString.Append("([Target].[System.WorkItemType] = 'User Story' AND [Target].[System.State] = 'Active') ORDER BY [System.Id] mode(Recursive)");
                    
                    Query wiQuery = new Query(wiStore, queryString.ToString());
                    
                    WorkItemLinkInfo[] wiTrees = wiQuery.RunLinkQuery();
                    
                    // Print the trees of user stories, with the estimated sizes of each leaf
                    PrintTrees(wiStore, wiTrees, "    ", 0, 0);
                    */
                }
            }

            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        // Each WorkItemLinkInfo structure in the collection contains the IDs of the linked work items.
        // In this case, the sourceId is the ID of the user story that is on the parent side of the link, and
        // the targetId is the ID of the user story that is on the child side of the link. The links
        // are returned in depth-first order. This function recursively traverses the collection
        // and the title of each user story. If the user story has no children, its estimation is also printed. my second edit
        static int PrintTrees(WorkItemStore wiStore, WorkItemLinkInfo[] wiTrees, string prefix, int sourceId, int iThis)
        {
            int iNext = 0;

            // Get the parent of this user story, if it has one
            WorkItem source = null;
            if (sourceId != 0)
            {
                source = wiStore.GetWorkItem(wiTrees[iThis].SourceId);
            }

            // Process the items in the list that have the same parent as this user story
            while (iThis < wiTrees.Length && wiTrees[iThis].SourceId == sourceId)
            {
                // Get this user story
                WorkItem target = wiStore.GetWorkItem(wiTrees[iThis].TargetId);
                Console.Write(prefix);
                Console.Write(target.Type.Name);
                Console.Write(": ");
                Console.Write(target.Fields["Title"].Value);
                if (target.Type.Name ==  "Product Backlog Item")
                {
                    Console.Write("; Effort = ");
                    Console.WriteLine(target.Fields["Effort"].Value);

                    foreach (Revision r in target.Revisions)//iterate through every revision of each work item
                    {
                        foreach (Field f in r.Fields)//iterate through every field of each revision
                        {
                                Console.WriteLine(f.Name + "\t" + f.Value);
                        }
                        Console.WriteLine();
                    }
                }

                if (target.Type.Name == "Task")
                {
                    Console.Write("; Remaining Work = ");
                    Console.WriteLine(target.Fields["Remaining Work"].Value);

                    foreach (Revision r in target.Revisions)//iterate through every revision of each work item
                    {
                        foreach (Field f in r.Fields)//iterate through every field of each revision
                        {
                                Console.WriteLine(f.Name + "\t" + f.Value);
                        }
                        Console.WriteLine();
                    }
                }

                if (iThis < wiTrees.Length - 1)
                {
                    if (wiTrees[iThis].TargetId == wiTrees[iThis + 1].SourceId)
                    {
                        // The next item is this user story's child. Process the children
                        Console.WriteLine();
                        iNext = PrintTrees(wiStore, wiTrees, prefix + "    ", wiTrees[iThis + 1].SourceId, iThis + 1);
                    }
                    else
                    {
                        // The next item is not this user story's child.
                        Console.Write("; estimate = ");
                        Console.WriteLine(target.Fields["Remaining Work"].Value);
                        
                        iNext = iThis + 1;
                    }
                }
                else
                {
                    // This user story is the last one.
                    iNext = iThis + 1;
                }

                iThis = iNext;
            }

            return iNext;
        }
    }
}
