module External

open Octokit
open Env
open Util


let github = GitHubClient(ProductHeaderValue("fscribe"))

github.Credentials <- Credentials(GetEnv "GITHUB_TOKEN")

async {
    let! user = github.User.Current() |> Async.AwaitTask
    logInfo "GitHub client login to %s." user.Name
}
|> Async.RunSynchronously

let targetOwner = GetEnv "SCRIBE_REPO_OWNER"
let targetRepo = GetEnv "SCRIBE_REPO_NAME"
let targetTag = GetEnv "SCRIBE_ISSUE_TAG"

let CommentCollected content =
    async {
        let filter = RepositoryIssueRequest()
        filter.Labels.Add targetTag
        filter.Filter <- IssueFilter.All
        filter.SortProperty <- IssueSort.Created
        filter.SortDirection <- SortDirection.Descending

        let! issues =
            github.Issue.GetAllForRepository(targetOwner, targetRepo, filter)
            |> Async.AwaitTask

        match issues |> Seq.tryHead with
        | None -> logWarning "No issue tagged with %s found in %s/%s." targetTag targetOwner targetRepo
        | Some issue ->
            logInfo "Found issue named %s, creating comment." issue.Title

            let! comment =
                github.Issue.Comment.Create(targetOwner, targetRepo, issue.Number, content)
                |> Async.AwaitTask

            logInfo "Comment created at %s." comment.HtmlUrl

        return ()
    }
