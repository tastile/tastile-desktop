use std::convert::Infallible;
use std::net::SocketAddr;
use std::sync::{Arc, Mutex};
use hyper::{Body, Request, Response, Server};
use hyper::service::{make_service_fn, service_fn};

#[derive(Clone)]
struct State {
    connected: Arc<Mutex<bool>>,
}

async fn handle(req: Request<Body>, state: State) -> Result<Response<Body>, Infallible> {
    let path = req.uri().path();
    
    let body = match path {
        "/health" => r#"{"status":"ok"}"#,
        "/status" => r#"{"status":"running","version":"0.1.0","active_tile_id":"tile-002","phase_kind":"work","phase_started_at":"2025-01-15T10:00:00Z","tile_count":3}"#,
        "/read/tiles" => r#"{"tiles":[{"id":"tile-001","title":"Design API","lifecycle":"ready","next_action":"Draw diagram","done_definition":"Reviewed","worked_minutes":0},{"id":"tile-002","title":"Implement core","lifecycle":"started","next_action":"Write tests","done_definition":"Passing","worked_minutes":45},{"id":"tile-003","title":"Setup CI","lifecycle":"ready","next_action":"Config actions","done_definition":"Deployed","worked_minutes":0}]}"#,
        "/read/active-tile" => r#"{"tile":{"id":"tile-002","title":"Implement core","lifecycle":"started","next_action":"Write tests","done_definition":"Passing","worked_minutes":45},"phase":"work","phase_started_at":"2025-01-15T10:00:00Z"}"#,
        "/read/execution" => r#"{"active_tile_id":"tile-002","phase_kind":"work","phase_started_at":"2025-01-15T10:00:00Z","phase_ends_at":null}"#,
        _ => r#"{"ok":true}"#,
    };
    
    Ok(Response::builder()
        .header("Content-Type", "application/json")
        .body(Body::from(body))
        .unwrap())
}

#[tokio::main]
async fn main() {
    let addr = SocketAddr::from(([127, 0, 0, 1], 3140));
    let state = State { connected: Arc::new(Mutex::new(true)) };
    
    let make_svc = make_service_fn(move |_conn| {
        let state = state.clone();
        async move {
            Ok::<_, Infallible>(service_fn(move |req| {
                handle(req, state.clone())
            }))
        }
    });
    
    let server = Server::bind(&addr).serve(make_svc);
    println!("Mock Tastile API Server on http://localhost:3140/");
    
    if let Err(e) = server.await {
        eprintln!("Server error: {}", e);
    }
}
