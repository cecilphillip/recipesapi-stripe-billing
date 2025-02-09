# Recipes API with Stripe usage based billing
This HTTP API sample shows how to implement usage based billing with Stripe and ASP.NET Core.

The application uses the [fixed fee and overage pricing model](https://docs.stripe.com/billing/subscriptions/usage-based/pricing-models#fixed-fee-overage) for subscriptions. A fixed fee is charged upfront and the overage fee is charged at the beginning of the next billing cycle.


## Getting Setup

### Prerequisites
- [.NET SDK](https://get.dot.net/) 9.0 or later
- [Stripe account](https://dashboard.stripe.com) & [Stripe CLI](https://stripe.com/docs/stripe-cli)
- [Docker](https://www.docker.com)
- (Optional) [Task](https://taskfile.dev)

### Running the solution
* Add your [Stripe Secret key](https://dashboard.stripe.com/apikeys)  to the Recipe.Api's [appsettings.Development.json](./src/Recipes.Api/appsettings.json) file.
* Run the [App Host](./src/Recipes.AppHost) project

### API Endpoints
All of the recipe resource endpoints are secured with an access token. The application will bootstrap a default set of
users, but you can register your own with the `POST /auth/register` endpoint. Here's a sample payload:

```json
{
  "email": "demo@demo.com",
  "password": "demo",
  "FullName": "Demo User"
}
```

You can generate an access token for a user by calling the `POST /auth/authenticate` endpoint with the following request:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=demo@demo.com&password=demo
```

Available endpoints
- `POST /api/recipes` - Create a recipe
- `GET /api/recipes/random` - Retrieve a random recipe
- `GET /api/recipes/latest` - Retrieve the latest recipes
- `GET /api/recipes/category/{category}` - Retrieve all recipes in a category
- `GET /api/recipes/code/{lookupCode}` - Retrieve a recipe with the specified lookup code
- `DELETE /api/recipes/code/{lookupCode}` - Remove a recipe with the specified lookup code

### Available Task Commands
If you install the Task task runner, you can run the following operations against the project using the syntax `task <task-name>`. 
For example, `task requests:generate` will generate a configurable number of request traffic to the API.

`requests:authenticate` - Get and set an access token for the API
```shell
  task requests:authenticate
```

`requests:generate` - Generate requests to the API
```shell
  task requests:generate
```

